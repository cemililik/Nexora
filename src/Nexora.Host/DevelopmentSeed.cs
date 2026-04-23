using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Npgsql;

namespace Nexora.Host;

/// <summary>
/// Provisions the development tenant, schema, and seed data on first startup.
/// Only runs in Development environment. Idempotent — safe to call on every startup.
/// </summary>
public static class DevelopmentSeed
{
    private static readonly Guid DevTenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DevOrgGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string DevTenantSlug = "nexora-dev";
    private const string DevTenantName = "Nexora Development";
    private const string SchemaName = "tenant_00000000-0000-0000-0000-000000000001";

    /// <summary>Seeds the development environment with a tenant, schema, and base data.</summary>
    public static async Task SeedAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DevelopmentSeed));

        logger.LogInformation("[DevSeed] Starting development tenant provisioning...");

        var connectionString = app.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing 'Default' connection string in configuration");

        try
        {
            // Step 1: Ensure platform tables exist (public schema)
            await EnsurePlatformTablesAsync(app.Services, connectionString, logger);

            // Step 1b: Ensure outbox table exists (public schema)
            await EnsureOutboxTableAsync(app.Services, connectionString, logger);

            // Step 2: Insert dev tenant record
            await EnsureDevTenantAsync(connectionString, logger);

            // Step 3: Create tenant schema
            await EnsureTenantSchemaAsync(connectionString, logger);

            // Step 4: Create all module tables in tenant schema
            await EnsureIdentityTablesAsync(app.Services, connectionString, logger);
            await EnsureModuleTablesAsync<ContactsDbContext>(app.Services, connectionString, "contacts_contacts", logger);
            await EnsureModuleTablesAsync<DocumentsDbContext>(app.Services, connectionString, "documents_documents", logger);
            await EnsureModuleTablesAsync<NotificationsDbContext>(app.Services, connectionString, "notifications_templates", logger);
            await EnsureModuleTablesAsync<ReportingDbContext>(app.Services, connectionString, "reporting_report_definitions", logger);
            await EnsureModuleTablesAsync<AuditDbContext>(app.Services, connectionString, "audit_entries", logger);

            // Step 5: Apply incremental schema changes (new columns added after initial table creation)
            await ApplySchemaUpdatesAsync(connectionString, logger);

            // Step 6: Seed permissions, roles, organization, tenant record
            await SeedIdentityDataAsync(app.Services, connectionString, app.Configuration, logger);

            // Step 7: Register all modules for the dev tenant
            await EnsureTenantModulesAsync(connectionString, logger);

            logger.LogInformation("[DevSeed] Development tenant provisioning complete");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DevSeed] Failed to provision development tenant");
            throw;
        }
    }

    private static async Task EnsurePlatformTablesAsync(IServiceProvider rootSp, string connectionString, ILogger logger)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Check if platform tables already exist
        var exists = await TableExistsAsync(conn, "public", "platform_tenants");
        if (exists)
        {
            logger.LogInformation("[DevSeed] Platform tables already exist");
            return;
        }

        // Use PlatformDbContext's model to create tables via IRelationalDatabaseCreator
        using var scope = rootSp.CreateScope();
        var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var creator = platformDb.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();

        logger.LogInformation("[DevSeed] Platform tables created");
    }

    private static async Task EnsureOutboxTableAsync(IServiceProvider rootSp, string connectionString, ILogger logger)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var exists = await TableExistsAsync(conn, "public", "outbox_messages");
        if (exists)
        {
            logger.LogInformation("[DevSeed] Outbox table already exists");
            return;
        }

        using var scope = rootSp.CreateScope();
        var outboxDb = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        var creator = outboxDb.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();

        logger.LogInformation("[DevSeed] Outbox table created in public schema");
    }

    private static async Task EnsureDevTenantAsync(string connectionString, ILogger logger)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO public."platform_tenants" ("Id", "Name", "Slug", "Status", "RealmId", "CreatedAt")
            VALUES (@id, @name, @slug, @status, @realm, @created)
            ON CONFLICT ("Id") DO NOTHING
            """;
        cmd.Parameters.AddWithValue("id", DevTenantGuid);
        cmd.Parameters.AddWithValue("name", DevTenantName);
        cmd.Parameters.AddWithValue("slug", DevTenantSlug);
        cmd.Parameters.AddWithValue("status", "Active");
        cmd.Parameters.AddWithValue("realm", "nexora-dev");
        cmd.Parameters.AddWithValue("created", DateTimeOffset.UtcNow);

        var rows = await cmd.ExecuteNonQueryAsync();
        logger.LogInformation("[DevSeed] Dev tenant: {Status}", rows > 0 ? "created" : "already exists");
    }

    private static async Task EnsureTenantSchemaAsync(string connectionString, ILogger logger)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Schema name is a compile-time constant, safe for interpolation
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""CREATE SCHEMA IF NOT EXISTS "{SchemaName}" """;
        await cmd.ExecuteNonQueryAsync();

        logger.LogInformation("[DevSeed] Tenant schema ensured: {Schema}", SchemaName);
    }

    private static async Task EnsureIdentityTablesAsync(IServiceProvider rootSp, string connectionString, ILogger logger)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Check if identity tables already exist in tenant schema
        var exists = await TableExistsAsync(conn, SchemaName, "identity_users");
        if (exists)
        {
            logger.LogInformation("[DevSeed] Identity tables already exist in tenant schema");
            return;
        }

        // Set tenant context so IdentityDbContext resolves to the correct schema
        using var scope = rootSp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(DevTenantGuid.ToString(), DevOrgGuid.ToString());

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new IdentityDbContext(options, accessor);
        var creator = dbContext.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();

        logger.LogInformation("[DevSeed] Identity tables created in tenant schema");
    }

    private static async Task SeedIdentityDataAsync(IServiceProvider rootSp, string connectionString, IConfiguration configuration, ILogger logger)
    {
        using var scope = rootSp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(DevTenantGuid.ToString(), DevOrgGuid.ToString());

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new IdentityDbContext(options, accessor);

        // Seed tenant record in identity schema
        if (!await dbContext.Tenants.AnyAsync())
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO "{SchemaName}"."identity_tenants" ("Id", "Name", "Slug", "Status", "RealmId", "CreatedAt")
                VALUES (@id, @name, @slug, @status, @realm, @created)
                ON CONFLICT DO NOTHING
                """;
            cmd.Parameters.AddWithValue("id", DevTenantGuid);
            cmd.Parameters.AddWithValue("name", DevTenantName);
            cmd.Parameters.AddWithValue("slug", DevTenantSlug);
            cmd.Parameters.AddWithValue("status", "Active");
            cmd.Parameters.AddWithValue("realm", "nexora-dev");
            cmd.Parameters.AddWithValue("created", DateTimeOffset.UtcNow);
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("[DevSeed] Seeded tenant record in identity schema");
        }

        // Seed default organization with the known GUID (matches Keycloak org_id claim)
        if (!await dbContext.Organizations.AnyAsync())
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO "{SchemaName}"."identity_organizations" ("Id", "TenantId", "Name", "Slug", "Timezone", "DefaultCurrency", "DefaultLanguage", "IsActive", "CreatedAt")
                VALUES (@id, @tid, @name, @slug, @tz, @cur, @lang, @active, @created)
                ON CONFLICT DO NOTHING
                """;
            cmd.Parameters.AddWithValue("id", DevOrgGuid);
            cmd.Parameters.AddWithValue("tid", DevTenantGuid);
            cmd.Parameters.AddWithValue("name", DevTenantName);
            cmd.Parameters.AddWithValue("slug", DevTenantSlug);
            cmd.Parameters.AddWithValue("tz", "UTC");
            cmd.Parameters.AddWithValue("cur", "USD");
            cmd.Parameters.AddWithValue("lang", "en");
            cmd.Parameters.AddWithValue("active", true);
            cmd.Parameters.AddWithValue("created", DateTimeOffset.UtcNow);
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("[DevSeed] Seeded default organization: {OrgId}", DevOrgGuid);
        }

        // Permissions + Platform Admin role are owned by IdentityModuleMigration.SeedAsync
        // (the single source of truth per ADR-004 / permissions.md §3 / T-020). Delegating
        // here keeps dev and prod seed paths in sync — no parallel permission list to drift.
        var identityModuleMigration = scope.ServiceProvider
            .GetRequiredService<IdentityModuleMigration>();
        await identityModuleMigration.SeedAsync(SchemaName);
        logger.LogInformation(
            "[DevSeed] Delegated permission + Platform Admin role seed to IdentityModuleMigration");

        // Seed admin user (matches Keycloak test user)
        await SeedAdminUserAsync(dbContext, connectionString, configuration, logger);
    }

    private static async Task SeedAdminUserAsync(IdentityDbContext dbContext, string connectionString, IConfiguration configuration, ILogger logger)
    {
        // Query Keycloak for the admin user's UUID (sub claim)
        string? keycloakUserId = null;
        try
        {
            using var http = new HttpClient();

            var keycloakUsername = configuration["DevSeed:KeycloakAdminUsername"] ?? "admin";
            var keycloakPassword = configuration["DevSeed:KeycloakAdminPassword"]
                ?? throw new InvalidOperationException("DevSeed:KeycloakAdminPassword is not configured");

            // Get admin token from Keycloak master realm
            var tokenForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "admin-cli",
                ["username"] = keycloakUsername,
                ["password"] = keycloakPassword,
                ["grant_type"] = "password",
            });
            var tokenResp = await http.PostAsync("http://keycloak:8080/realms/master/protocol/openid-connect/token", tokenForm);
            if (!tokenResp.IsSuccessStatusCode)
            {
                logger.LogWarning("[DevSeed] Cannot reach Keycloak admin API — skipping admin user seed");
                return;
            }

            var tokenJson = await tokenResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var adminToken = tokenJson.GetProperty("access_token").GetString();

            // Look up admin@nexora.dev user
            using var req = new HttpRequestMessage(HttpMethod.Get,
                "http://keycloak:8080/admin/realms/nexora-dev/users?username=admin@nexora.dev");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
            var usersResp = await http.SendAsync(req);

            if (!usersResp.IsSuccessStatusCode)
            {
                logger.LogWarning("[DevSeed] Keycloak user lookup failed — skipping admin user seed");
                return;
            }

            var usersJson = await usersResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            if (usersJson.GetArrayLength() > 0)
                keycloakUserId = usersJson[0].GetProperty("id").GetString();
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "[DevSeed] Keycloak not reachable — skipping admin user seed");
            return;
        }

        if (string.IsNullOrEmpty(keycloakUserId))
        {
            logger.LogWarning("[DevSeed] Keycloak user admin@nexora.dev not found — skipping admin user seed");
            return;
        }

        // Check if user already exists
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.KeycloakUserId == keycloakUserId);

        if (existingUser is not null)
        {
            logger.LogInformation("[DevSeed] Admin user already exists: {UserId}", existingUser.Id);
            return;
        }

        // Create admin user
        var tenantId = TenantId.From(DevTenantGuid);
        var user = User.Create(tenantId, keycloakUserId, "admin@nexora.dev", "Platform", "Admin");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        // Create organization membership
        var orgId = OrganizationId.From(DevOrgGuid);
        var orgUser = OrganizationUser.Create(user.Id, orgId, isDefault: true);
        await dbContext.OrganizationUsers.AddAsync(orgUser);

        // Assign Platform Admin role
        var adminRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.IsSystemRole);
        if (adminRole is not null)
        {
            var userRole = UserRole.Create(orgUser.Id, adminRole.Id);
            await dbContext.UserRoles.AddAsync(userRole);
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("[DevSeed] Seeded admin user (Keycloak: {KeycloakId})", keycloakUserId);
    }

    private static async Task EnsureModuleTablesAsync<TContext>(
        IServiceProvider rootSp, string connectionString, string checkTable, ILogger logger)
        where TContext : DbContext
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var exists = await TableExistsAsync(conn, SchemaName, checkTable);
        if (exists)
        {
            logger.LogInformation("[DevSeed] {Module} tables already exist in tenant schema", typeof(TContext).Name);
            return;
        }

        using var scope = rootSp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(DevTenantGuid.ToString(), DevOrgGuid.ToString());

        var options = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = (TContext?)Activator.CreateInstance(typeof(TContext), options, accessor, null)
            ?? throw new InvalidOperationException($"Failed to create {typeof(TContext).Name} instance");
        var creator = dbContext.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();

        logger.LogInformation("[DevSeed] {Module} tables created in tenant schema", typeof(TContext).Name);
    }

    /// <summary>
    /// Applies incremental schema changes for columns added after initial table creation.
    /// Uses IF NOT EXISTS to be idempotent — safe to run on every startup.
    /// </summary>
    /// <remarks>
    /// Development-only: incremental schema changes applied via ALTER TABLE for rapid iteration.
    /// In production, these changes MUST be managed via EF Core migrations.
    /// See: docs/standards/INFRASTRUCTURE_STANDARDS.md for migration guidelines.
    /// </remarks>
    private static async Task ApplySchemaUpdatesAsync(string connectionString, ILogger logger)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Set search_path once, then use plain table names (avoids SQL injection scanner false positive)
        await using (var setPath = conn.CreateCommand())
        {
            setPath.CommandText = $"SET search_path TO \"{SchemaName}\"";
            await setPath.ExecuteNonQueryAsync();
        }

        var alterStatements = new[]
        {
            // Inbox table for idempotent integration event consumption (used by all consumer modules)
            """
            CREATE TABLE IF NOT EXISTS inbox_messages (
                "EventId" uuid PRIMARY KEY,
                "EventType" varchar(500) NOT NULL,
                "ProcessedAt" timestamptz NOT NULL DEFAULT now()
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_inbox_messages_ProcessedAt\" ON inbox_messages (\"ProcessedAt\")",
            // Outbox table for transactional event publishing (used by all producer modules)
            """
            CREATE TABLE IF NOT EXISTS outbox_messages (
                "Id" uuid PRIMARY KEY,
                "EventType" varchar(500) NOT NULL,
                "EventPayload" jsonb NOT NULL,
                "TenantId" varchar(100) NOT NULL,
                "CreatedAt" timestamptz NOT NULL DEFAULT now(),
                "ProcessedAt" timestamptz,
                "Error" text,
                "RetryCount" int NOT NULL DEFAULT 0
            )
            """,
            "CREATE INDEX IF NOT EXISTS ix_outbox_pending ON outbox_messages (\"CreatedAt\") WHERE \"ProcessedAt\" IS NULL",
            "CREATE INDEX IF NOT EXISTS ix_outbox_tenant ON outbox_messages (\"TenantId\", \"CreatedAt\") WHERE \"ProcessedAt\" IS NULL",
            // OrganizationUser.JoinedAt — added for member join date tracking
            "ALTER TABLE identity_organization_users ADD COLUMN IF NOT EXISTS \"JoinedAt\" timestamptz DEFAULT now()",
            // UserRole.AssignedAt — added for role assignment date tracking
            "ALTER TABLE identity_user_roles ADD COLUMN IF NOT EXISTS \"AssignedAt\" timestamptz DEFAULT now()",
            // FolderAccess — folder-level access control with optional expiration
            """
            CREATE TABLE IF NOT EXISTS documents_folder_accesses (
                "Id" uuid PRIMARY KEY,
                "FolderId" uuid NOT NULL,
                "UserId" uuid,
                "RoleId" uuid,
                "Permission" varchar(20) NOT NULL,
                "ExpiresAt" timestamptz,
                "CreatedAt" timestamptz NOT NULL DEFAULT now(),
                "CreatedBy" text,
                "UpdatedAt" timestamptz,
                "UpdatedBy" text,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAt" timestamptz,
                "DeletedBy" text
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_documents_folder_accesses_FolderId_UserId\" ON documents_folder_accesses (\"FolderId\", \"UserId\")",
            "CREATE INDEX IF NOT EXISTS \"IX_documents_folder_accesses_FolderId_RoleId\" ON documents_folder_accesses (\"FolderId\", \"RoleId\")",
            "CREATE INDEX IF NOT EXISTS \"IX_documents_folder_accesses_ExpiresAt\" ON documents_folder_accesses (\"ExpiresAt\") WHERE \"ExpiresAt\" IS NOT NULL",
            // Unique filtered index: prevents duplicate document names within the same folder (excluding soft-deleted)
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_documents_documents_TenantId_FolderId_Name\" ON documents_documents (\"TenantId\", \"FolderId\", \"Name\") WHERE \"IsDeleted\" = false",
            // Permission.Scope — Phase 1.5.2: classifies permissions as Platform or Tenant scope
            "ALTER TABLE identity_permissions ADD COLUMN IF NOT EXISTS \"Scope\" varchar(20) NOT NULL DEFAULT 'Tenant'",
            // Classify identity.tenants.* permissions as Platform scope so tenant admins cannot assign them to tenant roles
            "UPDATE identity_permissions SET \"Scope\"='Platform' WHERE \"Module\"='identity' AND \"Resource\"='tenants'",
            // User.PreferredLanguage — Phase 1.5.3: BCP 47 language tag for UI display preference (null = use tenant default)
            "ALTER TABLE identity_users ADD COLUMN IF NOT EXISTS \"PreferredLanguage\" varchar(10)",
            // Organization.DefaultLocale — Phase 1.5.3: IETF locale tag for org-level number/date formatting (e.g. "en-US", "tr-TR")
            "ALTER TABLE identity_organizations ADD COLUMN IF NOT EXISTS \"DefaultLocale\" varchar(20) NOT NULL DEFAULT 'en-US'",
            // User.ContactId — optional link to a Contacts module contact record for 360° view
            "ALTER TABLE identity_users ADD COLUMN IF NOT EXISTS \"ContactId\" uuid NULL",
            // TenantModule.DeletedTableNames — CSV of renamed tables captured at uninstall time, used by reinstall path
            "ALTER TABLE identity_tenant_modules ADD COLUMN IF NOT EXISTS \"DeletedTableNames\" text NULL",

            // --- ADR-0025: Org-scoped compliance configuration + policy audit trail ---
            // Three-tier resolver (platform cap → tenant default → org override) reads from these
            // tables. Both live in the tenant schema; `platform_` prefix is legacy naming consistent
            // with `platform_tenant_config` rather than an indication of schema placement.

            // Organization-scope override store for configuration keys (per ADR-0025).
            """
            CREATE TABLE IF NOT EXISTS platform_org_config (
                "OrganizationId" uuid NOT NULL,
                "Key" varchar(256) NOT NULL,
                "Value" jsonb NOT NULL,
                "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
                "UpdatedBy" varchar(200),
                PRIMARY KEY ("OrganizationId", "Key")
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_platform_org_config_Key\" ON platform_org_config (\"Key\")",

            // Append-only policy audit trail for compliance-config changes (per ADR-0025).
            """
            CREATE TABLE IF NOT EXISTS platform_compliance_policy_audit (
                "Id" uuid PRIMARY KEY,
                "TenantId" uuid NOT NULL,
                "OrganizationId" uuid,
                "Key" varchar(256) NOT NULL,
                "OldValue" jsonb,
                "NewValue" jsonb,
                "ChangedByUserId" uuid NOT NULL,
                "ChangedAtUtc" timestamptz NOT NULL DEFAULT now(),
                "Reason" varchar(500) NOT NULL DEFAULT ''
            )
            """,
            // Legacy tenants may predate the Reason column entirely. Add it as a nullable
            // varchar so fresh installs (CREATE TABLE above already enforces NOT NULL) and
            // legacy tenants converge without violating schema-migration.md §2 rule 2
            // (no ALTER COLUMN tightening nullability). Any legacy NULL rows remain legal;
            // new writes from the resolver always carry a non-null Reason.
            "ALTER TABLE platform_compliance_policy_audit ADD COLUMN IF NOT EXISTS \"Reason\" varchar(500) DEFAULT ''",
            "CREATE INDEX IF NOT EXISTS \"IX_platform_compliance_policy_audit_TenantId_ChangedAtUtc\" ON platform_compliance_policy_audit (\"TenantId\", \"ChangedAtUtc\")",
            "CREATE INDEX IF NOT EXISTS \"IX_platform_compliance_policy_audit_Key\" ON platform_compliance_policy_audit (\"Key\")",
            // Compliance-review lookup: recent changes for a given org + key.
            "CREATE INDEX IF NOT EXISTS \"IX_platform_compliance_policy_audit_OrgKey_ChangedAt\" ON platform_compliance_policy_audit (\"OrganizationId\", \"Key\", \"ChangedAtUtc\" DESC)",

            // --- Contacts module: tables added after the initial CreateTables short-circuit ---
            // (EnsureModuleTablesAsync only runs CreateTablesAsync once per sentinel; entities
            //  added to the DbContext later never reach that path, so their tables must be
            //  declared here with CREATE TABLE IF NOT EXISTS per docs/standards/schema-migration.md)

            // ImportJob — tracks contact import jobs (Hangfire-backed, bulk queue)
            """
            CREATE TABLE IF NOT EXISTS contacts_import_jobs (
                "Id" uuid PRIMARY KEY,
                "TenantId" uuid NOT NULL,
                "OrganizationId" uuid NOT NULL,
                "FileName" varchar(500) NOT NULL,
                "FileFormat" varchar(10) NOT NULL,
                "StorageKey" varchar(1000) NOT NULL,
                "Status" varchar(20) NOT NULL,
                "TotalRows" int NOT NULL DEFAULT 0,
                "ProcessedRows" int NOT NULL DEFAULT 0,
                "SuccessCount" int NOT NULL DEFAULT 0,
                "ErrorCount" int NOT NULL DEFAULT 0,
                "SkippedCount" int NOT NULL DEFAULT 0,
                "ErrorDetails" jsonb,
                "HangfireJobId" varchar(100),
                "ColumnMappingJson" text,
                "CreatedBy" varchar(200),
                "CreatedAt" timestamptz NOT NULL,
                "CompletedAt" timestamptz
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_contacts_import_jobs_TenantId_Status\" ON contacts_import_jobs (\"TenantId\", \"Status\")",
            "CREATE INDEX IF NOT EXISTS \"IX_contacts_import_jobs_TenantId_HangfireJobId\" ON contacts_import_jobs (\"TenantId\", \"HangfireJobId\")",

            // ExportJob — tracks contact export jobs (CSV/XLSX/vCard generation)
            """
            CREATE TABLE IF NOT EXISTS contacts_export_jobs (
                "Id" uuid PRIMARY KEY,
                "TenantId" uuid NOT NULL,
                "OrganizationId" uuid NOT NULL,
                "Format" varchar(10) NOT NULL,
                "StorageKey" varchar(1000),
                "Status" varchar(20) NOT NULL,
                "TotalRows" int NOT NULL DEFAULT 0,
                "ErrorDetails" jsonb,
                "HangfireJobId" varchar(100),
                "FiltersJson" text,
                "FieldsJson" text,
                "CreatedBy" varchar(200),
                "CreatedAt" timestamptz NOT NULL,
                "CompletedAt" timestamptz
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_contacts_export_jobs_TenantId_Status\" ON contacts_export_jobs (\"TenantId\", \"Status\")",
            "CREATE INDEX IF NOT EXISTS \"IX_contacts_export_jobs_TenantId_HangfireJobId\" ON contacts_export_jobs (\"TenantId\", \"HangfireJobId\")",

            // TenantConfigEntry — per-tenant key/value config store read by ITenantConfiguration.
            // Despite the "platform_" prefix, the table lives inside each tenant's schema
            // (TenantConfigDbContext uses HasDefaultSchema(schema)); the prefix is legacy naming.
            """
            CREATE TABLE IF NOT EXISTS platform_tenant_config (
                "Key" varchar(256) PRIMARY KEY,
                "Value" jsonb NOT NULL,
                "UpdatedAt" timestamptz NOT NULL DEFAULT now()
            )
            """,

            // GdprErasureAudit — append-only forensic trail for GDPR Article 17 erasures
            """
            CREATE TABLE IF NOT EXISTS contacts_gdpr_erasure_audit (
                "Id" uuid PRIMARY KEY,
                "TenantId" uuid NOT NULL,
                "ContactId" uuid NOT NULL,
                "ErasedByUserId" uuid NOT NULL,
                "ErasedAtUtc" timestamptz NOT NULL,
                "Reason" varchar(500) NOT NULL,
                "Mode" varchar(20) NOT NULL,
                "ChildCountsJson" text NOT NULL
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_contacts_gdpr_erasure_audit_TenantId_ErasedAtUtc\" ON contacts_gdpr_erasure_audit (\"TenantId\", \"ErasedAtUtc\")",
            "CREATE INDEX IF NOT EXISTS \"IX_contacts_gdpr_erasure_audit_ContactId\" ON contacts_gdpr_erasure_audit (\"ContactId\")",

            // ImportJob.SkippedCount — separates "already-exists" skips from genuine errors for accurate reporting.
            // Redundant when the CREATE TABLE above runs fresh (column already present), but required for
            // tenants whose contacts_import_jobs was created before SkippedCount was introduced.
            "ALTER TABLE contacts_import_jobs ADD COLUMN IF NOT EXISTS \"SkippedCount\" int NOT NULL DEFAULT 0",

            // --- T-017: Notifications BodyRendered becomes nullable ---
            // The GDPR scrub path now writes null (not "[REDACTED]") at end of hot
            // retention so a compliance auditor reading the table cannot mistake a
            // placeholder for real content. Dropping NOT NULL is additive per
            // schema-migration.md §2 rule 2 (nullability relaxation is allowed).
            "ALTER TABLE notifications_notifications ALTER COLUMN \"BodyRendered\" DROP NOT NULL",

            // --- T-005: Demo Data Framework idempotency marker (tenant schema) ---
            // Recorded per (TenantId, ModuleName, Scenario) after a module's
            // SeedDemoDataAsync completes successfully. Subsequent runs short-circuit.
            """
            CREATE TABLE IF NOT EXISTS platform_demo_seed_markers (
                "TenantId" uuid NOT NULL,
                "ModuleName" varchar(100) NOT NULL,
                "Scenario" varchar(50) NOT NULL,
                "SeededAt" timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY ("TenantId", "ModuleName", "Scenario")
            )
            """,
        };

        foreach (var sql in alterStatements)
        {
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                // Unique index creation may fail if duplicate data exists — skip gracefully.
                logger.LogWarning(ex,
                    "[DevSeed] Skipped schema update due to existing data conflict. {SkippedStatement}",
                    Truncate(sql, 120));
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                // Parent table does not exist yet — legitimate when a module's sentinel
                // short-circuited initial CreateTables, leaving a later-added entity's
                // table uncreated. The schema update is effectively a no-op for this
                // tenant until the owning table is (re)created. Safe to skip.
                logger.LogWarning(ex,
                    "[DevSeed] Skipped schema update because target relation is missing. {SkippedStatement}",
                    Truncate(sql, 120));
            }
        }

        logger.LogInformation("[DevSeed] Schema updates applied ({Count} statements)", alterStatements.Length);
    }

    /// <summary>Collapses a SQL statement to its first line for compact log output.</summary>
    private static string Truncate(string sql, int max)
    {
        var firstLine = sql.Split('\n', 2)[0].Trim();
        return firstLine.Length <= max ? firstLine : firstLine[..max] + "…";
    }

    private static async Task EnsureTenantModulesAsync(string connectionString, ILogger logger)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var moduleNames = new[] { "identity", "contacts", "documents", "notifications", "reporting", "audit" };

        foreach (var moduleName in moduleNames)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO public."platform_tenant_modules" ("Id", "TenantId", "ModuleName", "InstalledAt", "IsActive")
                VALUES (@id, @tid, @name, @at, @active)
                ON CONFLICT DO NOTHING
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("tid", DevTenantGuid);
            cmd.Parameters.AddWithValue("name", moduleName);
            cmd.Parameters.AddWithValue("at", DateTimeOffset.UtcNow);
            cmd.Parameters.AddWithValue("active", true);
            await cmd.ExecuteNonQueryAsync();
        }

        logger.LogInformation("[DevSeed] Tenant modules registered: {Modules}", string.Join(", ", moduleNames));
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection conn, string schema, string table)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS(
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = @schema AND table_name = @table
            )
            """;
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    // CreateDefaultPermissions() removed by T-020: permissions are declared once, by each
    // module's OnStartupAsync, and materialized into the DB by IdentityModuleMigration.SeedAsync.
    // See ADR-004 and docs/standards/permissions.md §3.
}
