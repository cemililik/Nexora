# Nexora - Module System Architecture

## 1. Overview

Nexora's module system is a **true plugin architecture** — modules are self-contained units that can be installed, configured, and removed per tenant without affecting other modules or tenants. The platform runs with only the core modules; everything else is optional.

This is NOT just logical separation — it's **runtime modularity**:
- A tenant without the Donations module will have **no donation tables, no donation endpoints, no donation UI**.
- Installing a module creates its database tables, registers its API routes, seeds its default data, and enables its UI components.
- Uninstalling a module archives its data, removes its routes, and hides its UI.

## 2. Module Contract

Every module implements the `IModule` interface:

```csharp
public interface IModule
{
    /// Unique module identifier (e.g., "donations", "crm", "education")
    string Name { get; }

    /// Human-readable display name
    string DisplayName { get; }

    /// Module version (SemVer)
    string Version { get; }

    /// Modules that must be installed before this one
    IReadOnlyList<string> Dependencies { get; }

    /// Register services (DI container)
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    /// Register API endpoints / controllers
    void MapEndpoints(IEndpointRouteBuilder endpoints);

    /// Register event handlers (Kafka consumers)
    void ConfigureEventHandlers(IEventBusBuilder builder);

    /// Register background jobs (Hangfire)
    void ConfigureJobs(IJobScheduler scheduler);

    /// Run when module is installed for a tenant (create tables, seed data)
    Task OnInstallAsync(TenantContext tenant, CancellationToken ct);

    /// Run when module is uninstalled (archive data, cleanup)
    Task OnUninstallAsync(TenantContext tenant, CancellationToken ct);

    /// Run on application startup (register permissions, etc.)
    Task OnStartupAsync(CancellationToken ct);

    /// Health check for this module
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct);
}
```

### Example: Donations Module Registration

```csharp
public sealed class DonationsModule : IModule
{
    public string Name => "donations";
    public string DisplayName => "Donations & Fundraising";
    public string Version => "1.0.0";
    public IReadOnlyList<string> Dependencies => ["identity", "contacts", "notifications"];

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDonationRepository, DonationRepository>();
        services.AddScoped<IPaymentGateway, StripePaymentGateway>();
        services.AddScoped<DonationsDbContext>();
        // MediatR handlers auto-discovered from this assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DonationsModule).Assembly));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/donations")
            .RequireAuthorization()
            .WithTags("Donations");

        group.MapDonationEndpoints();
        group.MapCampaignEndpoints();
        group.MapRecurringEndpoints();
        group.MapBankImportEndpoints();
    }

    public void ConfigureEventHandlers(IEventBusBuilder builder)
    {
        builder.Subscribe<ContactMergedEvent, DonationContactMergeHandler>();
        builder.Subscribe<OrganizationCreatedEvent, SeedDonationCategoriesHandler>();
    }

    public async Task OnInstallAsync(TenantContext tenant, CancellationToken ct)
    {
        // 1. Run EF migrations for donation tables in tenant schema
        await using var db = CreateDbContext(tenant);
        await db.Database.MigrateAsync(ct);

        // 2. Seed default data
        await SeedDefaultCategories(db, tenant.OrganizationId, ct);

        // 3. Register module permissions
        await RegisterPermissions(tenant, ct);
    }

    public async Task OnUninstallAsync(TenantContext tenant, CancellationToken ct)
    {
        // 1. Archive data (don't drop tables — mark as archived)
        await ArchiveModuleData(tenant, ct);

        // 2. Remove permissions
        await RemovePermissions(tenant, ct);

        // 3. Cancel active recurring plans
        await CancelActiveRecurringPlans(tenant, ct);
    }
}
```

## 3. Module Lifecycle

```mermaid
---
title: Module Installation Lifecycle
---
stateDiagram-v2
    [*] --> Available: Module exists in registry
    Available --> Installing: Tenant admin installs
    Installing --> Installed: Migration + seed successful
    Installing --> Failed: Installation error
    Failed --> Available: Retry / fix
    Installed --> Active: Module operational
    Active --> Uninstalling: Tenant admin removes
    Uninstalling --> Archived: Data archived, routes removed
    Archived --> Installing: Reinstall (restore archived data)
    Active --> Updating: New version available
    Updating --> Active: Migration successful
    Updating --> Active: Rollback on failure

    note right of Active: Endpoints registered\nEvents consumed\nJobs scheduled
    note right of Archived: Data preserved\nEndpoints removed\nEvents unsubscribed
```

## 4. Module Discovery & Loading

### Startup Flow

```mermaid
---
title: Application Startup - Module Loading
---
sequenceDiagram
    participant Host as Nexora.Host
    participant Loader as ModuleLoader
    participant Registry as Module Registry (DB)
    participant Module as IModule

    Host->>Loader: DiscoverModules()
    Loader->>Loader: Scan assemblies for IModule implementations
    Loader-->>Host: List of available modules

    loop For each discovered module
        Host->>Module: ConfigureServices()
        Note over Host: DI registration (all modules,<br/>regardless of tenant installation)
    end

    Host->>Host: Build application

    loop For each discovered module
        Host->>Module: OnStartupAsync()
        Module->>Registry: Register available permissions
        Module->>Module: Initialize module-level caches
    end

    Host->>Host: Application ready
```

### Per-Request Module Resolution

```mermaid
---
title: Per-Request Module Availability Check
---
sequenceDiagram
    participant Client
    participant APISIX as APISIX Gateway
    participant MW as TenantMiddleware
    participant ModMW as ModuleMiddleware
    participant Cache as Redis Cache
    participant Endpoint as Module Endpoint

    Client->>APISIX: GET /api/v1/donations/donations
    APISIX->>MW: Forward (JWT validated)
    MW->>MW: Resolve tenant from JWT
    MW->>ModMW: Next middleware

    ModMW->>ModMW: Extract module name from route ("donations")
    ModMW->>Cache: Is "donations" installed for tenant?

    alt Module installed
        Cache-->>ModMW: Yes, active
        ModMW->>Endpoint: Forward to handler
        Endpoint-->>Client: 200 OK
    else Module not installed
        Cache-->>ModMW: No
        ModMW-->>Client: 404 {"error": {"code": "lockey_error_module_not_installed"}}
    end
```

## 5. Module Database Strategy

Each module owns its tables within the tenant schema. Tables are prefixed with the module name to avoid collisions:

```sql
-- Schema: tenant_isabet
-- Identity module tables
CREATE TABLE identity_users (...);
CREATE TABLE identity_roles (...);
CREATE TABLE identity_permissions (...);

-- Contacts module tables
CREATE TABLE contacts_contacts (...);
CREATE TABLE contacts_tags (...);
CREATE TABLE contacts_addresses (...);

-- Donations module tables (only exist if module installed)
CREATE TABLE donations_donations (...);
CREATE TABLE donations_categories (...);
CREATE TABLE donations_recurring_plans (...);

-- CRM module tables (only exist if module installed)
CREATE TABLE crm_leads (...);
CREATE TABLE crm_pipelines (...);
```

### Migration Strategy

```mermaid
---
title: Module Migration per Tenant
---
flowchart TB
    Install["Module Install\nfor Tenant X"]
    Install --> Check["Check current\nmigration state"]
    Check --> Apply["Apply pending\nmigrations to\ntenant_x schema"]
    Apply --> Seed["Seed default\ndata"]
    Seed --> Permissions["Register\npermissions"]
    Permissions --> Cache["Invalidate\ntenant cache"]
    Cache --> Done["Module Active"]

    style Install fill:#3498db,color:#fff
    style Done fill:#27ae60,color:#fff
```

Each module has its own `DbContext` and migration history:

```csharp
public class DonationsDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // Dynamic schema based on current tenant
        options.UseNpgsql(connectionString);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Set schema to current tenant
        builder.HasDefaultSchema(_tenant.SchemaName);

        // Prefix all tables
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            entity.SetTableName($"donations_{entity.GetTableName()}");
        }

        // Apply module-specific configurations
        builder.ApplyConfigurationsFromAssembly(typeof(DonationsDbContext).Assembly);
    }
}
```

## 6. Cross-Module Communication

Modules **NEVER** reference each other directly. All communication is via:

### 6.1 Integration Events (Async, via Kafka)

```csharp
// Contacts module publishes:
public sealed record ContactMergedIntegrationEvent(
    Guid OldContactId,
    Guid NewContactId,
    Guid TenantId) : IIntegrationEvent;

// Donations module subscribes (only if installed):
public sealed class DonationContactMergeHandler
    : IIntegrationEventHandler<ContactMergedIntegrationEvent>
{
    public async Task Handle(ContactMergedIntegrationEvent @event, CancellationToken ct)
    {
        // Update donor_contact_id from old to new
        await _repository.UpdateContactReferences(
            @event.OldContactId, @event.NewContactId, ct);
    }
}
```

### 6.2 Module Query Interface (Sync, in-process)

For cases where a module needs data from another module synchronously, we use **shared interfaces** defined in SharedKernel:

```csharp
// Defined in SharedKernel (not in any module)
public interface IContactQueryService
{
    Task<ContactSummary?> GetContactSummaryAsync(Guid contactId, CancellationToken ct);
}

// Implemented by Contacts module
public sealed class ContactQueryService : IContactQueryService { ... }

// Used by Donations module (resolved via DI)
public sealed class CreateDonationHandler(IContactQueryService contactQuery)
{
    public async Task Handle(CreateDonationCommand cmd, CancellationToken ct)
    {
        var donor = await contactQuery.GetContactSummaryAsync(cmd.DonorId, ct);
        if (donor is null)
            return Result.Failure("lockey_error_donor_not_found");
        // ...
    }
}
```

### 6.3 Module Feature Contribution

Modules can contribute data to shared views (e.g., Contact 360-view):

```csharp
// Defined in SharedKernel
public interface IContactActivityContributor
{
    string ModuleName { get; }
    Task<IReadOnlyList<ActivityItem>> GetActivitiesAsync(
        Guid contactId, DateRange range, CancellationToken ct);
    Task<object?> GetSummaryAsync(Guid contactId, CancellationToken ct);
}

// Donations module registers its contributor
public sealed class DonationActivityContributor : IContactActivityContributor
{
    public string ModuleName => "donations";

    public async Task<object?> GetSummaryAsync(Guid contactId, CancellationToken ct)
    {
        return new DonorSummary
        {
            TotalDonated = await _repo.GetTotalDonatedAsync(contactId, ct),
            LastDonation = await _repo.GetLastDonationAsync(contactId, ct),
            IsRecurringDonor = await _repo.HasActiveRecurringPlanAsync(contactId, ct)
        };
    }
}

// Contact module collects all contributors at runtime
public sealed class Contact360ViewHandler(
    IEnumerable<IContactActivityContributor> contributors)
{
    // Only installed modules will have registered contributors
    // If Donations is not installed, no DonationActivityContributor in DI
}
```

## 7. Module UI Integration

### Admin Panel (React)

Modules register their UI routes and navigation items dynamically:

```typescript
// Each module exposes a manifest
export const donationsModule: ModuleManifest = {
  name: 'donations',
  displayName: 'Donations & Fundraising',
  icon: 'Heart',
  routes: [
    { path: '/donations', component: lazy(() => import('./pages/DonationList')) },
    { path: '/donations/:id', component: lazy(() => import('./pages/DonationDetail')) },
    { path: '/donations/campaigns', component: lazy(() => import('./pages/Campaigns')) },
    // ...
  ],
  navigation: [
    { label: 'lockey_nav_donations', path: '/donations', icon: 'Heart' },
    { label: 'lockey_nav_campaigns', path: '/donations/campaigns', icon: 'Target' },
    { label: 'lockey_nav_recurring', path: '/donations/recurring', icon: 'Repeat' },
  ],
  permissions: ['donations.donations.read'], // required to see in nav
};
```

### Module Loading in React

```typescript
// App-level module loader
const ModuleRouter: React.FC = () => {
  const { installedModules } = useTenantModules(); // from API

  const activeModules = allModuleManifests.filter(m =>
    installedModules.includes(m.name)
  );

  return (
    <Routes>
      {activeModules.flatMap(mod =>
        mod.routes.map(route => (
          <Route
            key={route.path}
            path={route.path}
            element={
              <RequirePermission permissions={mod.permissions}>
                <Suspense fallback={<ModuleSkeleton />}>
                  <route.component />
                </Suspense>
              </RequirePermission>
            }
          />
        ))
      )}
      <Route path="*" element={<NotFound />} />
    </Routes>
  );
};
```

### Portal (Next.js)

Portal pages are conditionally rendered based on installed modules:

```typescript
// Donor portal - only shows sections for installed modules
const DonorDashboard: React.FC = () => {
  const { hasModule } = useModules();

  return (
    <DashboardLayout>
      {hasModule('donations') && <DonationHistory />}
      {hasModule('sponsorship') && <MySponsorships />}
      {hasModule('education') && <MyChildren />}
    </DashboardLayout>
  );
};
```

## 8. Module Install/Uninstall API

### Admin Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/identity/modules/available` | List all available modules |
| GET | `/api/v1/identity/modules/installed` | List installed modules for current tenant |
| POST | `/api/v1/identity/modules/install` | Install module for tenant |
| POST | `/api/v1/identity/modules/uninstall` | Uninstall module for tenant |
| GET | `/api/v1/identity/modules/{name}/health` | Module health check |

### Install Request

```json
POST /api/v1/identity/modules/install
{
  "moduleName": "donations",
  "organizationIds": ["org-1", "org-2"]  // which orgs get default roles
}
```

### Install Response

```json
{
  "data": {
    "moduleName": "donations",
    "status": "installed",
    "version": "1.0.0",
    "installedAt": "2026-03-19T10:00:00Z",
    "tablesCreated": 8,
    "permissionsRegistered": 24,
    "defaultRolesCreated": ["Donation Manager", "Donation Viewer"]
  }
}
```

## 9. Module Dependency Resolution

```mermaid
---
title: Module Dependency Resolution on Install
---
flowchart TB
    Request["Install: Sponsorship"] --> Resolve["Resolve Dependencies"]
    Resolve --> Check{"All deps\ninstalled?"}
    Check -->|Yes| Install["Install Sponsorship"]
    Check -->|No| Missing["Missing:\n- donations (not installed)"]
    Missing --> Auto{"Auto-install\ndependencies?"}
    Auto -->|Yes| Chain["Install chain:\n1. donations\n2. sponsorship"]
    Auto -->|No| Error["Error:\nlockey_error_missing_dependencies\n{deps: ['donations']}"]

    style Request fill:#3498db,color:#fff
    style Error fill:#e74c3c,color:#fff
    style Chain fill:#27ae60,color:#fff
```

### Rules
- **Cannot install** a module if its dependencies are not installed
- **Cannot uninstall** a module if other installed modules depend on it
- **Can force-uninstall** with confirmation (cascades to dependents)
- Core modules (Identity, Contacts) **cannot be uninstalled**

## 10. Module Registry

The following table lists all Nexora modules, their phases, and dependency profiles:

| # | Module | Name (ID) | Phase | Required Dependencies | Optional Dependencies | Core? |
|---|--------|-----------|-------|----------------------|----------------------|-------|
| 1 | Identity & Access | `identity` | Core | — | — | Yes |
| 2 | Contact Management | `contacts` | Core | identity | — | Yes |
| 3 | Notification Engine | `notifications` | Core | identity, contacts | — | Yes |
| 4 | Document Management | `documents` | Core | identity | — | No |
| 5 | Reporting Engine | `reporting` | Core | identity | contacts, notifications, documents | No |
| 6 | Portal Framework | `portal` | Core | identity | — | No |
| 7 | CRM | `crm` | Phase 2 | contacts, notifications | — | No |
| 8 | Donations & Fundraising | `donations` | Phase 2 | contacts, notifications, documents | — | No |
| 9 | Sponsorship | `sponsorship` | Phase 2 | contacts, donations, notifications | — | No |
| 10 | Event Management | `events` | Phase 2 | contacts, notifications | documents, crm, donations | No |
| 11 | Collection Box (Kumbara) | `kumbara` | Phase 2 | contacts, notifications | — | No |
| 12 | Aid Package (Kumanya) | `kumanya` | Phase 2 | contacts, notifications | — | No |
| 13 | Education Management | `education` | Phase 3 | crm, contacts, documents, notifications | subscription | No |
| 14 | Subscription & Billing | `subscription` | Phase 3 | contacts, notifications | reporting, documents | No |
| 15 | Website & CMS | `cms` | Phase 3 | notifications | contacts, crm, donations, portal | No |
| 16 | Surveys & Feedback | `surveys` | Phase 3 | contacts, notifications | — | No |
| 17 | Accounting & Finance | `accounting` | Phase 4 | contacts | hr, documents, notifications | No |
| 18 | HR & Payroll | `hr` | Phase 4 | contacts, notifications, documents | — | No |
| 19 | Point of Sale | `pos` | Phase 4 | contacts, notifications | inventory, accounting | No |
| 20 | Fleet Management | `fleet` | Phase 4 | contacts, notifications, documents | — | No |
| 21 | Inventory & Assets | `inventory` | Phase 4 | contacts, notifications | documents | No |
| 22 | Project Management | `projects` | Phase 4 | contacts, notifications | documents, accounting | No |

> **Note**: All modules implicitly depend on `identity` for authentication, tenant resolution, and RBAC. This is not listed as a separate dependency since Identity is the foundational module that must always be present.

#### Module Specifications
| Module | Spec |
|--------|------|
| Identity & Access | [SPEC.md](../modules/identity/SPEC.md) |
| Contact Management | [SPEC.md](../modules/contacts/SPEC.md) |
| Notification Engine | [SPEC.md](../modules/notifications/SPEC.md) |
| Document Management | [SPEC.md](../modules/documents/SPEC.md) |
| Reporting Engine | [SPEC.md](../modules/reporting/SPEC.md) |
| CRM | [SPEC.md](../modules/crm/SPEC.md) |
| Donations & Fundraising | [SPEC.md](../modules/donations/SPEC.md) |

### Module Classification

- **Core modules** (Identity, Contacts) cannot be uninstalled and are always available.
- **Required dependencies** must be installed before the dependent module. The system will block installation if dependencies are missing.
- **Optional dependencies** enable additional features when present. The module uses `IModuleAvailability` to check at runtime:

```csharp
// Check if an optional dependency is available
public sealed class CreateEventHandler(
    IModuleAvailability modules,
    IEventRepository repository)
{
    public async Task Handle(CreateEventCommand cmd, CancellationToken ct)
    {
        var @event = Event.Create(cmd.Name, cmd.StartDate, cmd.EndDate);

        // Optional: link to CRM pipeline if CRM module is installed
        if (modules.IsInstalled("crm") && cmd.CrmCampaignId.HasValue)
        {
            @event.LinkToCampaign(cmd.CrmCampaignId.Value);
        }

        repository.Add(@event);
    }
}
```

For the full dependency graph with visual diagrams, see [Module Dependencies](../diagrams/module-dependencies.md).

## 11. Module Marketplace (Future — Phase 4+)

The plugin architecture is designed to support third-party modules in the future:

```
nexora-marketplace/
├── Official Modules (by Nexora team)
├── Partner Modules (certified third-party)
└── Community Modules (unverified)
```

Each module packaged as a NuGet package + npm package (UI), installable via admin panel.
