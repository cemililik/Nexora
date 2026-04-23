# Module: Reporting Engine

> Updated 2026-04-22 — added Tax Receipt & Financial Document Templates section.

**Tier:** 1 — Platform Core

> **Status**: Implemented
> **Module Name**: `reporting`
> **Version**: `1.0.0`
> **Tier**: Core/Platform (always installed)
> **Dependencies**: `identity` (required)
> **Optional Dependencies**: `contacts`, `notifications`, `documents` (for cross-module SQL joins and scheduled email delivery)

## Overview
The Reporting module provides SQL-based report definitions, on-demand and scheduled execution, multi-format export (CSV, Excel, PDF, JSON), and dashboard analytics across all Nexora modules. Reports execute within the tenant's PostgreSQL schema using read-only transactions, with results stored in MinIO. It supports parameterized queries, cron-based scheduling, and a widget-based dashboard builder.

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
| `ReportParameterDefinition` | Parameter spec: name, type (String/Number/Date/Boolean/Money), required, defaultValue. `Money` parameters bind to `SharedKernel.Money` (amount + ISO currency) — see ADR-0021; never split into separate `amount` + `currency` parameters |
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

## Architecture Diagrams

### C4 Component Diagram

```mermaid
C4Component
    title Reporting Module - Component Diagram

    Container_Boundary(reporting, "Reporting Module") {
        Component(defMgmt, "ReportDefinition Management", "Application Service", "CRUD operations for report definitions with SQL validation")
        Component(execEngine, "Report Execution Engine", "Application Service", "Executes SQL queries, exports results as Stream to CSV/Excel/PDF/JSON via ReportExportService, uploads to storage")
        Component(scheduler, "Report Scheduling", "Application Service", "Cron-based scheduling via Hangfire, dispatches executions for due schedules")
        Component(dashMgmt, "Dashboard Management", "Application Service", "CRUD for dashboards and widget-based layout with live data")
        Component(sqlValidator, "SQL Query Validator", "Domain Service", "Validates SQL safety: SELECT/WITH only, blocks DML/DDL, prevents injection")
        Component(fileStorage, "File Storage Client", "Infrastructure", "Uploads and retrieves report files from object storage")
        Component(dbAccess, "Database Access", "Infrastructure", "Read-only tenant-scoped SQL execution via Dapper")
    }

    Rel(defMgmt, sqlValidator, "Validates query text")
    Rel(execEngine, sqlValidator, "Re-validates before execution")
    Rel(execEngine, dbAccess, "Executes report SQL")
    Rel(execEngine, fileStorage, "Uploads exported files")
    Rel(scheduler, execEngine, "Creates executions for due schedules")
    Rel(dashMgmt, execEngine, "Fetches widget data")

    ContainerDb(postgres, "PostgreSQL", "Tenant Schema", "Report definitions, executions, schedules, dashboards")
    Container(minio, "MinIO", "Object Storage", "nexora-reports bucket for exported files")

    Rel(dbAccess, postgres, "READ ONLY transactions")
    Rel(fileStorage, minio, "PutObject / GetObject")
```

### Integration Diagram

```mermaid
flowchart TB
    subgraph reporting["Reporting Module"]
        defApi["Report Definitions API"]
        execApi["Report Executions API"]
        schedApi["Report Schedules API"]
        dashApi["Dashboards API"]
        execEngine["Execution Engine"]
    end

    subgraph identity["Identity Module"]
        authn["Authentication"]
        authz["Authorization"]
        tenant["Tenant Context"]
    end

    subgraph contacts["Contacts Module"]
        contactDb[("contacts_* tables")]
    end

    subgraph documents["Documents Module"]
        docStorage["Document Storage"]
    end

    subgraph notifications["Notifications Module"]
        notifService["Notification Service"]
    end

    %% Identity integration
    authn -- "JWT validation" --> defApi
    authn -- "JWT validation" --> execApi
    authn -- "JWT validation" --> schedApi
    authn -- "JWT validation" --> dashApi
    authz -- "Permission checks\n(reporting.*)" --> defApi
    authz -- "Permission checks\n(reporting.*)" --> execApi
    authz -- "Permission checks\n(reporting.*)" --> schedApi
    authz -- "Permission checks\n(reporting.*)" --> dashApi
    tenant -- "Tenant schema\n(search_path)" --> execEngine

    %% Contacts integration
    execEngine -- "SQL joins on\ncontact data" --> contactDb

    %% Documents integration
    execEngine -. "Potential storage\nof generated reports" .-> docStorage

    %% Notifications integration
    execEngine -- "ReportExecutionCompletedEvent\n(scheduled report email delivery)" --> notifService
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
| `DELETE` | `/{id}` | Delete definition (returns 200 OK with ApiEnvelope) |
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
| `DELETE` | `/{id}` | Delete schedule (returns 200 OK with ApiEnvelope) |

### Dashboards (`/api/v1/reporting/dashboards`)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/` | List dashboards |
| `GET` | `/{id}` | Get single dashboard |
| `POST` | `/` | Create dashboard |
| `PUT` | `/{id}` | Update dashboard (name, widgets, isDefault) |
| `DELETE` | `/{id}` | Delete dashboard (returns 200 OK with ApiEnvelope) |
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
| `ScheduledReportDispatcherJob` | Every 15 minutes | `default` | Finds due schedules using Cronos for cron expression parsing, creates executions |

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

## Tax Receipt & Financial Document Templates

### Purpose

Reporting hosts locale-aware receipt and financial-document templates that other modules
invoke. This keeps document layout concerns out of domain modules and lets templates be
revised without touching business logic.

### Supported Template Categories (Tier-1 Baseline)

| Template code | Description | Trigger |
|---------------|-------------|---------|
| `donation_receipt_tr` | Turkish bağış makbuzu (Vakıflar Genel Müdürlüğü / Diyanet compatible layout) | `Fundraising.DonationConfirmed` |
| `donation_receipt_us` | US 501(c)(3) tax-deductible donation receipt | `Fundraising.DonationConfirmed` (selected by tenant default currency + jurisdiction) |
| `annual_donor_statement_tr` | Turkish year-end aggregate donor statement | Scheduled job (not event-triggered) |
| `annual_donor_statement_us` | US year-end aggregate donor statement | Scheduled job (not event-triggered) |
| `invoice_tr` | Turkish subscription invoice PDF | `Subscription.InvoiceIssued` |
| `invoice_us` | US subscription invoice PDF | `Subscription.InvoiceIssued` |

> e-Fatura compatibility is a `TODO(maintainer)` — current scope is the human-readable
> PDF, not the GIB XML envelope.

### Template Selection Rule

Fundraising/Subscription publishes an event carrying `tenant_id`, `locale_hint`, and
`jurisdiction_hint`. Reporting resolves the template as follows:

1. Explicit template override in tenant settings wins.
2. Otherwise the `(jurisdiction, locale)` pair matches a shipped template.
3. Otherwise fallback to `donation_receipt_default` (English, neutral layout).

### Template Variables Contract

Each template declares its required variables (e.g. donor name, donor tax id, `amount`,
category, campaign, `receipt_number`). The caller module supplies these in the event
payload or a follow-up query; Reporting validates and rejects with
`report.template.variable_missing` if any required variable is absent.

**Monetary variables — ADR-0021 compliance.** Monetary template variables MUST be typed
as `SharedKernel.Money` (amount + ISO 4217 currency code) — never as separate `amount`
(decimal) + `currency` (string) fields. Templates reference a single `{{amount}}` token
(or `{{total}}`, `{{subtotal}}`, etc. depending on template) which the renderer formats
as a localized, currency-symbolled string using the tenant's locale and the `Money`
value object's own ISO currency. For example, `Money(250.00, "TRY")` rendered under
`tr-TR` emits `250,00 ₺`; under `en-US` with `Money(250.00, "USD")` it emits `$250.00`.
Callers MUST NOT pass `{{currency}}` as a separate token — the renderer derives the
symbol/code from the `Money` instance.

Non-monetary decimals (percentages, counts, ratios, tax rates) remain plain `decimal`
and are not affected by this rule.

### Rendered Output Storage

Rendered PDFs are persisted via the Documents module (`documents.documents.create`) under
a system folder keyed by `category=tax-receipt` and linked back to the originating entity
(donation id, invoice id). Reporting returns the `document_id` to the caller; the caller
links it in its own record. See [Documents SPEC](../documents/SPEC.md) for the template
rendering interop surface.

### i18n

Templates use `lockey_reporting_receipt_*` keys for UI labels; jurisdiction boilerplate
(tax deductibility notice, legal entity footer) lives in the template body itself (not
`lockey_` — jurisdiction-bound, not UI-label). See
[Localization Standards](../../../standards/localization.md).

### Cross-Module Events

| Direction | Event | Effect |
|-----------|-------|--------|
| Consumed | `Fundraising.DonationConfirmed` | Triggers `donation_receipt_{locale}` rendering |
| Consumed | `Subscription.InvoiceIssued` | Triggers `invoice_{locale}` rendering |
| Produced | `reporting.document.rendered` | Carries `template_code`, `document_id`, `correlation_id`, `tenant_id` |

See [Fundraising SPEC §5](../../tier-3a-ngo/fundraising/SPEC.md) for the donor-facing
contract.

### Receipt Rendering Sequence

```mermaid
sequenceDiagram
    participant FR as Fundraising
    participant BUS as Event Bus
    participant REP as Reporting (Template Renderer)
    participant DOC as Documents
    participant NOT as Notifications

    FR->>BUS: publish DonationConfirmed {tenant_id, locale_hint, jurisdiction_hint, vars}
    BUS->>REP: DonationConfirmed
    REP->>REP: Resolve template (override → jurisdiction+locale → default)
    REP->>REP: Validate required variables
    REP->>REP: Render PDF
    REP->>DOC: documents.documents.create (category=tax-receipt, linked entity=donation_id)
    DOC-->>REP: document_id
    REP->>BUS: publish reporting.document.rendered {template_code, document_id, correlation_id, tenant_id}
    BUS->>NOT: reporting.document.rendered
    NOT->>NOT: Send email receipt with document attached
```

### TODO(maintainer)

- e-Fatura XML envelope path (GIB requirement for TR corporate invoices above the
  statutory threshold).
- Additional locales (AR / FR) — scheduled for Phase 3+ based on tenant demand.
- Legal review sign-off workflow on template body changes.

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

`ReportExportService` returns a `Stream` for all formats (not `byte[]`), enabling efficient streaming to storage and HTTP responses without buffering entire files in memory.

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
