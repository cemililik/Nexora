# Module: Reporting Engine

## Overview
The Reporting module provides SQL-based report definitions, on-demand and scheduled execution, multi-format export (CSV, Excel, PDF, JSON), and dashboard analytics across all Nexora modules. Reports execute within the tenant's PostgreSQL schema using read-only transactions, with results stored in MinIO. It supports parameterized queries, cron-based scheduling, and a widget-based dashboard builder.

**Module Name**: `reporting`
**Version**: `1.0.0`
**Dependencies**: `identity` (required)
**Optional Dependencies**: `contacts`, `notifications`, `documents` (for cross-module SQL joins and scheduled email delivery)

## Domain Model

### Entities

```mermaid
---
title: Reporting Module - Entity Relationship Diagram
---
erDiagram
    ReportDefinition ||--o{ ReportExecution : "has executions"
    ReportDefinition ||--o{ ReportSchedule : "has schedules"
    Dashboard ||--o{ DashboardWidget : "contains widgets (JSON)"
    DashboardWidget }o--|| ReportDefinition : "references"

    ReportDefinition {
        uuid id PK
        uuid tenant_id FK
        uuid organization_id FK
        string name
        string description "nullable"
        string module "e.g. contacts, crm"
        string category "nullable"
        string query_text "SQL query"
        jsonb parameters "ReportParameterDefinition[]"
        enum default_format "Csv, Excel, Pdf, Json"
        boolean is_active
        timestamp created_at
        string created_by
    }

    ReportExecution {
        uuid id PK
        uuid tenant_id FK
        uuid definition_id FK
        enum status "Queued, Running, Completed, Failed"
        jsonb parameter_values "nullable"
        string result_storage_key "MinIO path"
        enum format "Csv, Excel, Pdf, Json"
        int row_count "nullable"
        long duration_ms "nullable"
        string error_details "nullable, max 4000"
        string executed_by "nullable"
        string hangfire_job_id "nullable"
        timestamp created_at
    }

    ReportSchedule {
        uuid id PK
        uuid tenant_id FK
        uuid definition_id FK
        string cron_expression "e.g. 0 9 * * MON"
        enum format "Csv, Excel, Pdf, Json"
        jsonb recipients "email addresses"
        boolean is_active
        timestamp last_execution_at "nullable"
        timestamp next_execution_at "nullable"
        timestamp created_at
    }

    Dashboard {
        uuid id PK
        uuid tenant_id FK
        uuid organization_id FK
        string name
        string description "nullable"
        boolean is_default
        jsonb widgets "DashboardWidget[]"
        timestamp created_at
        string created_by
    }
```

### Value Objects

| Type | Description |
|------|-------------|
| `ReportDefinitionId` | Strongly-typed GUID for report definitions |
| `ReportExecutionId` | Strongly-typed GUID for executions |
| `ReportScheduleId` | Strongly-typed GUID for schedules |
| `DashboardId` | Strongly-typed GUID for dashboards |
| `ReportParameterDefinition` | Parameter spec: name, type (String/Number/Date/Boolean), required, defaultValue |
| `DashboardWidget` | Widget spec: id, type, title, reportDefinitionId, chartType, position (x,y), size (w,h), config |

### Enums

| Enum | Values |
|------|--------|
| `ReportStatus` | `Queued`, `Running`, `Completed`, `Failed` |
| `ReportFormat` | `Csv`, `Excel`, `Pdf`, `Json` |
| `WidgetType` | `Chart`, `Kpi`, `Table` |
| `ChartType` | `Bar`, `Line`, `Pie`, `Area` |

### Domain Events

| Event | Trigger |
|-------|---------|
| `ReportExecutionCompletedEvent` | Execution marked as Completed |
| `ReportScheduleCreatedEvent` | New schedule created |

## State Diagrams

### Report Execution Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Queued: Create execution
    Queued --> Running: Job picks up
    Queued --> Failed: Definition not found
    Running --> Completed: SQL + export + upload success
    Running --> Failed: Exception during execution
    Completed --> [*]
    Failed --> [*]
```

### Report Definition Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Active: Create definition
    Active --> Inactive: Deactivate()
    Inactive --> Active: Activate()
    Active --> [*]: Delete
    Inactive --> [*]: Delete
```

## API Endpoints

All endpoints require authorization. Responses wrapped in `ApiEnvelope<T>`.

### Report Definitions (`/api/v1/reporting/definitions`)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/` | List definitions (paginated, filterable by module, category, search) |
| `GET` | `/{id}` | Get single definition |
| `POST` | `/` | Create definition (SQL validated by SqlQueryValidator) |
| `PUT` | `/{id}` | Update definition (SQL validated) |
| `DELETE` | `/{id}` | Delete definition |
| `POST` | `/test-query` | Test SQL with LIMIT 10, return preview results |

### Report Executions (`/api/v1/reporting/executions`)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/` | List executions (filterable by definitionId, status) |
| `GET` | `/{id}` | Get single execution |
| `POST` | `/` | Queue new execution (creates Hangfire job) |
| `GET` | `/{id}/download` | Get presigned download URL (1h expiry) |
| `GET` | `/{id}/file` | Stream file directly through API (PDF, CSV, etc.) |

### Report Schedules (`/api/v1/reporting/schedules`)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/` | List schedules (filterable by definitionId) |
| `POST` | `/` | Create schedule |
| `PUT` | `/{id}` | Update schedule (cron, format, recipients) |
| `DELETE` | `/{id}` | Delete schedule |

### Dashboards (`/api/v1/reporting/dashboards`)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/` | List dashboards |
| `GET` | `/{id}` | Get single dashboard |
| `POST` | `/` | Create dashboard |
| `PUT` | `/{id}` | Update dashboard (name, widgets, isDefault) |
| `DELETE` | `/{id}` | Delete dashboard |
| `GET` | `/{dashboardId}/widgets/{widgetId}/data` | Execute widget's SQL and return data |

## Sequence Diagrams

### Report Execution Flow

```mermaid
sequenceDiagram
    actor User
    participant Admin as nexora-admin
    participant API as Nexora API
    participant HF as Hangfire
    participant DB as PostgreSQL
    participant S3 as MinIO

    User->>Admin: Click "Run Report"
    Admin->>API: POST /executions
    API->>DB: Create execution (Queued)
    API->>HF: Enqueue ReportExecutionJob
    API-->>Admin: 201 Created

    HF->>DB: Mark Running
    HF->>DB: SET search_path TO tenant_xxx
    HF->>DB: Execute SQL (READ ONLY, 30s timeout)
    HF->>HF: Export to format (PDF/CSV/Excel/JSON)
    HF->>S3: Upload to nexora-reports bucket
    HF->>DB: Mark Completed (storageKey, rowCount, durationMs)

    User->>Admin: Click "Preview"
    Admin->>API: GET /executions/{id}/file
    API->>S3: GetObject
    API-->>Admin: Stream file bytes
    Admin->>Admin: Display in iframe (PDF) or pre (CSV/JSON)
```

### Test Query Flow

```mermaid
sequenceDiagram
    actor User
    participant Admin as nexora-admin
    participant API as Nexora API
    participant DB as PostgreSQL

    User->>Admin: Write SQL + click "Test Query"
    Admin->>API: POST /definitions/test-query
    API->>API: SqlQueryValidator.IsValid()
    alt Invalid SQL
        API-->>Admin: 400 (error message)
    else Valid SQL
        API->>DB: SET search_path + READ ONLY
        API->>DB: SELECT * FROM (query) LIMIT 10
        API-->>Admin: 200 (columns + rows)
        Admin->>Admin: Show preview table
    end
```

## SQL Security

Report queries are validated and sandboxed at multiple levels:

1. **SqlQueryValidator** (create, update, and execution time):
   - Query must start with `SELECT` or `WITH`
   - Forbidden keywords blocked: `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `CREATE`, `TRUNCATE`, `EXEC`, `EXECUTE`, `GRANT`, `REVOKE`, `MERGE`, `CALL`, `COPY`
   - Semicolons disallowed (prevents statement chaining)
   - Word boundary regex matching (avoids false positives in column names)

2. **Execution sandbox**:
   - `SET TRANSACTION READ ONLY` enforced at database level
   - `SET search_path TO 'tenant_{id}'` for tenant isolation
   - 30-second query timeout
   - Dapper parameterized queries (prevents SQL injection in parameters)

## Background Jobs

| Job | Schedule | Queue | Description |
|-----|----------|-------|-------------|
| `ReportExecutionJob` | On-demand (Hangfire enqueue) | `default` | Executes SQL, exports, uploads to MinIO |
| `ScheduledReportDispatcherJob` | Every 15 minutes | `default` | Finds due schedules, creates executions |

## Storage

- **Bucket**: `nexora-reports` (shared across tenants)
- **Key pattern**: `reports/{tenantId}/{executionId}.{extension}`
- **Bucket creation**: On-demand via `EnsureBucketExistsAsync`
- **Download**: API-proxied file streaming (`/file` endpoint) or presigned URL (`/download` endpoint)
- **Public endpoint**: Configurable `PublicEndpoint` in `MinioStorageOptions` for browser-accessible presigned URLs

## Frontend Components

### Pages

| Page | Route | Description |
|------|-------|-------------|
| `ReportListPage` | `/reporting/reports` | List definitions, create dialog with SQL editor |
| `ReportDetailPage` | `/reporting/reports/:id` | View definition, execution history, edit/delete, preview/download |
| `ReportScheduleListPage` | `/reporting/schedules` | List and manage schedules |
| `DashboardListPage` | `/reporting/dashboards` | List dashboards |
| `DashboardViewPage` | `/reporting/dashboards/:id` | View dashboard with widgets |

### Key Components

| Component | Description |
|-----------|-------------|
| `SqlEditor` | CodeMirror editor with PostgreSQL syntax highlighting |
| `SqlTestResult` | Table rendering test query results (columns + rows) |
| `ReportStatusBadge` | Color-coded status badge (Queued/Running/Completed/Failed) |
| `ReportParameterForm` | Dynamic form for report parameters |
| `ChartWidget` | Recharts-based chart (Bar, Line, Area, Pie) |
| `KpiWidget` | Single-value KPI display |
| `TableWidget` | Data table for dashboard |
| `DashboardGrid` | Positioned widget grid layout |

### Hooks

| Hook | Description |
|------|-------------|
| `useReportDefinitions` | List definitions with pagination/filters |
| `useReportDefinition` | Get single definition |
| `useCreateReportDefinition` | Create mutation |
| `useUpdateReportDefinition` | Update mutation |
| `useDeleteReportDefinition` | Delete mutation |
| `useTestReportQuery` | Test SQL query mutation |
| `useReportExecutions` | List executions |
| `useExecuteReport` | Queue execution mutation |
| `useReportFile` | Download file as blob |

## Permissions

| Permission | Description |
|------------|-------------|
| `reporting.definition.read` | View report definitions |
| `reporting.definition.manage` | Create, update, delete definitions |
| `reporting.execution.read` | View report executions |
| `reporting.execution.run` | Execute reports |
| `reporting.schedule.manage` | Manage report schedules |
| `reporting.dashboard.read` | View dashboards |
| `reporting.dashboard.manage` | Create, update, delete dashboards |

## Export Formats

| Format | Library | Content-Type |
|--------|---------|-------------|
| CSV | CsvHelper | `text/csv` |
| Excel | ClosedXML | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |
| PDF | QuestPDF | `application/pdf` |
| JSON | System.Text.Json | `application/json` |

## Localization

65 translation keys across `en` and `tr` locales covering:
- Navigation, labels, status badges
- Validation messages
- Toast notifications
- Error messages (invalid query, execution failures)
- Action buttons (execute, download, preview, edit, delete, test query)
