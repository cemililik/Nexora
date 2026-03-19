# Nexora - Tenant Provisioning & Operations

## 1. Overview

This document covers the complete lifecycle of a tenant in Nexora — from initial provisioning to eventual offboarding. It applies to all deployment models (Cloud, Dedicated, Self-Hosted) with model-specific differences noted.

```mermaid
---
title: Tenant Lifecycle Overview
---
stateDiagram-v2
    [*] --> Provisioning: Admin creates tenant
    Provisioning --> Trial: Auto-provisioning complete
    Trial --> Active: Payment confirmed / License activated
    Trial --> Expired: Trial period ended (no conversion)
    Expired --> Active: Late conversion
    Expired --> Terminated: Data retention expired
    Active --> Suspended: Payment failed / Policy violation
    Suspended --> Active: Issue resolved
    Suspended --> Terminated: Grace period expired
    Active --> Terminated: Customer requests offboarding
    Terminated --> [*]: Data purged after retention period

    note right of Provisioning: Schema created\nKeycloak realm created\nCore modules installed\nAdmin user invited
    note right of Active: Fully operational\nAll licensed modules available\nSLA active
    note right of Suspended: Read-only access\nNo new data creation\nAPI returns 403 for writes
    note right of Terminated: Data archived\nSchema dropped (after retention)\nKeycloak realm disabled
```

## 2. Tenant Provisioning Flow

### 2.1 Cloud (SaaS) — Automated Provisioning

End-to-end provisioning happens automatically when a new organization signs up or a platform admin creates a tenant.

```mermaid
---
title: SaaS Tenant Provisioning Flow
---
sequenceDiagram
    actor Admin as Platform Admin / Self-Service
    participant API as Nexora Admin API
    participant TPS as Tenant Provisioning Service
    participant KC as Keycloak
    participant PG as PostgreSQL
    participant Redis as Redis
    participant MinIO as MinIO
    participant DNS as DNS / Ingress
    participant Notif as Notification Engine

    Admin->>API: POST /api/v1/platform/tenants
    Note over Admin,API: {name, slug, plan, adminEmail,<br/>modules, locale, timezone}

    API->>API: Validate request & check slug uniqueness
    API->>TPS: CreateTenantCommand

    par Database Schema
        TPS->>PG: CREATE SCHEMA tenant_{slug}
        TPS->>PG: Run core module migrations
        TPS->>PG: Seed default data (roles, permissions, settings)
    and Keycloak Realm
        TPS->>KC: Create realm: tenant_{slug}
        TPS->>KC: Configure OIDC client (admin + portal)
        TPS->>KC: Set realm branding defaults
        TPS->>KC: Create admin user account
    and Object Storage
        TPS->>MinIO: Create bucket: tenant-{slug}
        TPS->>MinIO: Set bucket policy (tenant-scoped)
    and Cache
        TPS->>Redis: Cache tenant config
        TPS->>Redis: Cache installed modules list
    end

    TPS->>DNS: Register {slug}.nexora.app route

    loop For each licensed module
        TPS->>PG: Run module migrations in tenant schema
        TPS->>PG: Seed module default data
        TPS->>KC: Register module permissions
    end

    TPS->>Notif: Send welcome email to admin
    Note over Notif: Email contains:<br/>- Login URL<br/>- Temp password / magic link<br/>- Getting started guide

    TPS-->>API: TenantProvisionedEvent
    API-->>Admin: 201 Created {tenantId, loginUrl, status}
```

### 2.2 Provisioning Steps in Detail

#### Step 1: Database Schema Creation

```sql
-- 1. Create isolated schema for tenant
CREATE SCHEMA IF NOT EXISTS tenant_isabet;

-- 2. Set search path for migration context
SET search_path TO tenant_isabet;

-- 3. Core module tables (always created)
-- Identity tables
CREATE TABLE identity_users (...);
CREATE TABLE identity_roles (...);
CREATE TABLE identity_permissions (...);
CREATE TABLE identity_organizations (...);
CREATE TABLE identity_user_organizations (...);

-- Contact tables
CREATE TABLE contacts_contacts (...);
CREATE TABLE contacts_addresses (...);
CREATE TABLE contacts_tags (...);

-- Notification tables
CREATE TABLE notifications_templates (...);
CREATE TABLE notifications_notifications (...);

-- Document tables
CREATE TABLE documents_folders (...);
CREATE TABLE documents_documents (...);

-- 4. Module-specific tables (based on license)
-- Only if CRM module is licensed:
CREATE TABLE crm_leads (...);
CREATE TABLE crm_pipelines (...);
-- etc.
```

#### Step 2: Keycloak Realm Setup

```json
{
  "realm": "tenant_isabet",
  "displayName": "Isabet Academy",
  "enabled": true,
  "loginTheme": "nexora",
  "internationalizationEnabled": true,
  "supportedLocales": ["en", "tr", "ar"],
  "defaultLocale": "en",
  "clients": [
    {
      "clientId": "nexora-admin",
      "protocol": "openid-connect",
      "publicClient": true,
      "redirectUris": ["https://isabet.nexora.app/*"],
      "webOrigins": ["https://isabet.nexora.app"]
    },
    {
      "clientId": "nexora-portal",
      "protocol": "openid-connect",
      "publicClient": true,
      "redirectUris": ["https://isabet.nexora.app/portal/*"],
      "webOrigins": ["https://isabet.nexora.app"]
    }
  ],
  "roles": {
    "realm": [
      { "name": "tenant-admin", "description": "Full tenant administration" },
      { "name": "org-admin", "description": "Organization-level administration" }
    ]
  }
}
```

#### Step 3: Default Data Seeding

```csharp
public sealed class TenantSeedService(
    ITenantContext tenant,
    IIdentityDbContext identityDb)
{
    public async Task SeedDefaultsAsync(TenantProvisioningRequest request, CancellationToken ct)
    {
        // 1. Create default organization
        var org = Organization.Create(
            request.OrganizationName,
            request.Locale,
            request.Timezone);
        identityDb.Organizations.Add(org);

        // 2. Create default roles
        var adminRole = Role.CreateSystemRole("Admin", Permissions.All);
        var viewerRole = Role.CreateSystemRole("Viewer", Permissions.ReadOnly);
        identityDb.Roles.AddRange(adminRole, viewerRole);

        // 3. Create admin user (linked to Keycloak)
        var adminUser = User.CreateFromKeycloak(
            request.AdminEmail,
            request.KeycloakUserId,
            org.Id);
        adminUser.AssignRole(adminRole);
        identityDb.Users.Add(adminUser);

        // 4. Create default notification templates
        await SeedNotificationTemplates(tenant, ct);

        // 5. Seed module-specific defaults
        foreach (var moduleName in request.Modules)
        {
            var module = _moduleLoader.GetModule(moduleName);
            await module.OnInstallAsync(tenant.AsTenantContext(), ct);
        }

        await identityDb.SaveChangesAsync(ct);
    }
}
```

### 2.3 Self-Hosted — CLI-Based Provisioning

Self-hosted customers use the `nexora-cli` tool or admin API:

```bash
# Option 1: Interactive CLI
nexora tenant create \
  --name "Isabet Academy" \
  --slug isabet \
  --admin-email admin@isabet.org \
  --modules crm,donations,sponsorship,education \
  --locale tr \
  --timezone "Europe/Istanbul"

# Option 2: From YAML configuration
nexora tenant create --from-file tenant-config.yaml

# Option 3: Bulk provisioning (for migrations)
nexora tenant create --from-file tenants-batch.yaml --batch
```

**tenant-config.yaml**:
```yaml
tenant:
  name: "Isabet Academy"
  slug: "isabet"
  plan: "enterprise"
  locale: "tr"
  timezone: "Europe/Istanbul"

admin:
  email: "admin@isabet.org"
  firstName: "Admin"
  lastName: "User"
  temporaryPassword: true  # or use magic link

organizations:
  - name: "Isabet Academy"
    type: "education"
  - name: "Isabet Knowledge Foundation"
    type: "ngo"

modules:
  - crm
  - donations
  - sponsorship
  - education
  - subscription
  - documents
  - events

branding:
  primaryColor: "#1e3a5f"
  logo: "./assets/isabet-logo.png"
  favicon: "./assets/favicon.ico"

customDomain: "app.isabetacademy.org"  # optional
```

## 3. Module Installation Per Tenant

After initial provisioning, tenant admins can install/remove modules via the admin panel.

```mermaid
---
title: Module Installation Flow
---
sequenceDiagram
    actor Admin as Tenant Admin
    participant UI as Admin Panel
    participant API as Module API
    participant License as License Service
    participant Loader as Module Loader
    participant DB as PostgreSQL
    participant KC as Keycloak
    participant Cache as Redis

    Admin->>UI: Click "Install CRM Module"
    UI->>API: POST /api/v1/identity/modules/install {moduleName: "crm"}

    API->>License: Check module in license
    alt Module not in license
        License-->>API: ❌ Not licensed
        API-->>UI: 403 {error: "lockey_error_module_not_licensed"}
    end
    License-->>API: ✅ Licensed

    API->>Loader: Resolve dependencies
    alt Dependencies missing
        Loader-->>API: ❌ Missing: [contacts, notifications]
        API-->>UI: 422 {error: "lockey_error_missing_dependencies", deps: [...]}
    end

    API->>DB: BEGIN TRANSACTION
    API->>Loader: module.OnInstallAsync(tenant)
    Loader->>DB: Run CRM module migrations
    Loader->>DB: Seed default CRM data (pipeline, stages)
    Loader->>KC: Register CRM permissions
    Loader->>KC: Add permissions to existing roles
    API->>DB: COMMIT

    API->>Cache: Invalidate tenant module cache
    API->>Cache: Update installed modules list

    API-->>UI: 200 {status: "installed", version: "1.0.0"}
    UI->>UI: Reload navigation (CRM menu appears)
```

## 4. Tenant Configuration

### 4.1 Tenant Settings (Admin Panel)

```mermaid
---
title: Tenant Settings Structure
---
flowchart TB
    subgraph General["General Settings"]
        Name["Organization Name"]
        Logo["Logo & Branding"]
        Locale["Default Locale"]
        TZ["Timezone"]
        Currency["Default Currency"]
        Domain["Custom Domain"]
    end

    subgraph Security["Security Settings"]
        MFA["Enforce MFA"]
        Session["Session Timeout"]
        Password["Password Policy"]
        IP["IP Allowlist"]
        SSO["SSO / SAML Config"]
    end

    subgraph Modules["Module Settings"]
        Installed["Installed Modules"]
        ModConfig["Per-Module Config"]
        Webhooks["Webhook URLs"]
        API["API Keys"]
    end

    subgraph Data["Data Settings"]
        Retention["Data Retention Policy"]
        Backup["Backup Schedule"]
        Export["Bulk Data Export"]
        GDPR["GDPR / KVKK Settings"]
    end
```

### 4.2 Configuration Hierarchy

```
Platform Defaults (hardcoded / env vars)
  └── Deployment Config (Helm values / appsettings.json)
       └── Tenant Config (stored in tenant schema)
            └── Organization Config (per org within tenant)
                 └── User Preferences (per user)
```

Each level overrides the one above it. Example:
- Platform default: `sessionTimeout = 30min`
- Tenant config: `sessionTimeout = 60min` (overrides platform)
- User preference: `language = tr` (overrides tenant default of `en`)

## 5. Custom Domain Setup

### 5.1 Cloud — CNAME + Automatic TLS

```mermaid
---
title: Custom Domain Flow
---
sequenceDiagram
    actor Admin as Tenant Admin
    participant UI as Admin Panel
    participant API as Nexora API
    participant DNS as DNS Provider
    participant APISIX as APISIX Gateway
    participant Cert as Cert Manager

    Admin->>UI: Settings → Custom Domain: "app.isabetacademy.org"
    UI->>API: PUT /api/v1/identity/tenant/settings/domain
    API->>API: Generate DNS verification token

    API-->>Admin: "Add CNAME record:\napp.isabetacademy.org → isabet.nexora.app\nAdd TXT record: _nexora-verify.isabetacademy.org → token123"

    Admin->>DNS: Add CNAME + TXT records
    Note over DNS: DNS propagation (minutes to hours)

    loop Every 30 seconds (up to 24h)
        API->>DNS: Verify TXT record
    end

    API->>DNS: ✅ TXT record verified
    API->>APISIX: Add route for app.isabetacademy.org → tenant_isabet
    API->>Cert: Request TLS certificate (Let's Encrypt)
    Cert-->>APISIX: Install TLS cert

    API-->>Admin: ✅ Custom domain active with HTTPS
```

### 5.2 Self-Hosted — Manual Ingress

```yaml
# Customer adds to their values.yaml
ingress:
  enabled: true
  hosts:
    - host: nexora.mycompany.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: nexora-tls
      hosts:
        - nexora.mycompany.com
```

## 6. Backup & Disaster Recovery

### 6.1 Cloud — Automated Backups

| Component | Backup Strategy | Frequency | Retention |
|-----------|----------------|-----------|-----------|
| PostgreSQL | pg_dump per tenant schema + WAL archiving | Every 6 hours + continuous WAL | 30 days |
| MinIO | Cross-region replication + daily snapshots | Continuous + daily | 90 days |
| Keycloak | Realm export (JSON) | Daily | 30 days |
| Redis | RDB snapshots | Every 1 hour | 7 days |
| Kafka | Topic replication (factor=3) | Continuous | Per retention policy |

### 6.2 Tenant-Level Restore

```mermaid
---
title: Per-Tenant Restore Flow
---
sequenceDiagram
    actor Ops as Nexora Ops Team
    participant API as Admin API
    participant Backup as Backup Service
    participant PG as PostgreSQL
    participant KC as Keycloak

    Ops->>API: POST /api/v1/platform/tenants/{id}/restore {backupId}
    API->>API: Suspend tenant (read-only mode)
    API->>Backup: Fetch backup snapshot
    Backup-->>API: Schema dump + MinIO snapshot

    API->>PG: DROP SCHEMA tenant_isabet CASCADE
    API->>PG: Restore schema from backup
    API->>KC: Restore realm config from backup
    API->>API: Reactivate tenant

    API-->>Ops: ✅ Tenant restored to backup point
```

### 6.3 Self-Hosted — Customer Responsibility

```bash
# Backup script (provided by Nexora)
nexora backup create \
  --tenant isabet \
  --output /backups/isabet-$(date +%Y%m%d).tar.gz \
  --include-files  # includes MinIO data

# Restore
nexora backup restore \
  --tenant isabet \
  --from /backups/isabet-20260319.tar.gz
```

## 7. Monitoring & Observability

### 7.1 Per-Tenant Monitoring

```mermaid
---
title: Observability Stack
---
flowchart LR
    subgraph App["Nexora Application"]
        API["API Pods"]
        Worker["Workers"]
    end

    subgraph Observability["Observability"]
        OTel["OpenTelemetry\nCollector"]
        Grafana["Grafana\nDashboards"]
        Loki["Loki\nLogs"]
        Tempo["Tempo\nTraces"]
        Prom["Prometheus\nMetrics"]
        Alert["AlertManager"]
    end

    API -->|"traces, metrics, logs"| OTel
    Worker -->|"traces, metrics, logs"| OTel
    OTel --> Loki
    OTel --> Tempo
    OTel --> Prom
    Prom --> Alert
    Grafana --> Loki
    Grafana --> Tempo
    Grafana --> Prom
```

### 7.2 Key Dashboards

| Dashboard | Metrics | Alert Threshold |
|-----------|---------|----------------|
| Tenant Health | Request latency, error rate, active users | P99 > 2s, error rate > 1% |
| Module Usage | Installs, API calls per module per tenant | — |
| Database | Schema size, query latency, connection pool | Schema > 10GB, P95 > 500ms |
| Auth | Login success/failure, token issuance rate | Failed logins > 50/min |
| Background Jobs | Queue depth, processing time, failure rate | Queue > 1000, failure > 5% |
| Storage | Bucket size, upload/download rate | Bucket > 50GB |

### 7.3 Tenant Admin Dashboard

Tenant admins see their own operational metrics (filtered to their tenant) via the admin panel:
- Active users (daily/weekly/monthly)
- Module usage statistics
- Storage consumption
- API usage (if API access is licensed)
- Notification delivery rates

## 8. Scaling Strategy

### 8.1 Horizontal Scaling

```mermaid
---
title: Scaling Triggers
---
flowchart TB
    subgraph Metrics["Scaling Signals"]
        CPU["CPU > 70%"]
        Memory["Memory > 80%"]
        RPS["Requests > 500/s"]
        Queue["Job Queue > 500"]
    end

    subgraph Actions["Auto-Scale Actions"]
        HPA["HPA: Add API pods\n(3 → 10 max)"]
        WorkerScale["Worker pods\n(2 → 8 max)"]
        PGPool["PgBouncer:\nIncrease pool size"]
        RedisScale["Redis: Add\nread replicas"]
    end

    CPU --> HPA
    Memory --> HPA
    RPS --> HPA
    RPS --> PGPool
    Queue --> WorkerScale
    RPS --> RedisScale
```

### 8.2 Tenant Isolation Under Load

- **Noisy neighbor protection**: Rate limiting per tenant at APISIX layer
- **Database connection pooling**: PgBouncer with per-tenant connection limits
- **Background job priority**: Fair scheduling across tenants (no single tenant can starve others)
- **Large tenant migration**: If a tenant exceeds thresholds, migrate to dedicated PostgreSQL instance

## 9. Tenant Offboarding

```mermaid
---
title: Tenant Offboarding Process
---
sequenceDiagram
    actor Admin as Tenant Admin / Nexora Ops
    participant API as Admin API
    participant PG as PostgreSQL
    participant MinIO as MinIO
    participant KC as Keycloak
    participant Archive as Archive Storage

    Admin->>API: POST /api/v1/platform/tenants/{id}/terminate
    API->>API: Verify confirmation (require typed slug)
    API->>API: Set status = Terminating

    Note over API: Grace period: 30 days (data accessible, read-only)

    API->>API: After 30 days...

    par Data Export
        API->>PG: pg_dump tenant schema → export.sql.gz
        API->>MinIO: Archive tenant bucket → export-files.tar.gz
        API->>Archive: Store exports (retained for 90 days)
    end

    par Cleanup
        API->>PG: DROP SCHEMA tenant_{slug} CASCADE
        API->>MinIO: Delete tenant bucket
        API->>KC: Delete realm tenant_{slug}
    end

    API->>API: Set status = Terminated
    API-->>Admin: ✅ Tenant terminated, export available for 90 days
```

## 10. Operations Runbook

### 10.1 Common Operations

| Operation | Cloud | Self-Hosted |
|-----------|-------|-------------|
| Create tenant | Admin API / self-service | `nexora tenant create` CLI |
| Install module | Admin panel → Modules | Admin panel or CLI |
| Custom domain | Admin panel → Settings | Ingress YAML |
| Backup tenant | Automatic | `nexora backup create` |
| Restore tenant | Support ticket / API | `nexora backup restore` |
| Suspend tenant | Admin API | `nexora tenant suspend` |
| Scale up | Automatic (HPA) | Manual: increase replicas in values.yaml |
| Update platform | Automatic (rolling) | `helm upgrade nexora ...` |
| View logs | Grafana/Loki | `nexora logs --tenant isabet` or own Grafana |
| Health check | Status page | `nexora health` |

### 10.2 CLI Reference

```bash
# Tenant management
nexora tenant list
nexora tenant create --from-file config.yaml
nexora tenant info isabet
nexora tenant suspend isabet --reason "payment overdue"
nexora tenant activate isabet
nexora tenant terminate isabet --confirm

# Module management
nexora module list --tenant isabet
nexora module install crm --tenant isabet
nexora module uninstall surveys --tenant isabet

# Backup & restore
nexora backup create --tenant isabet --output ./backup.tar.gz
nexora backup restore --tenant isabet --from ./backup.tar.gz
nexora backup list --tenant isabet

# Diagnostics
nexora health
nexora health --tenant isabet
nexora logs --tenant isabet --module donations --since 1h
nexora metrics --tenant isabet

# License
nexora license info
nexora license activate LICENSE_KEY
nexora license modules  # list available modules per license
```
