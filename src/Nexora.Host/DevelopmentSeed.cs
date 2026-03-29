using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Npgsql;
using Serilog;

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

        Log.Information("[DevSeed] Starting development tenant provisioning...");

        var connectionString = app.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing 'Default' connection string in configuration");

        try
        {
            // Step 1: Ensure platform tables exist (public schema)
            await EnsurePlatformTablesAsync(app.Services, connectionString);

            // Step 2: Insert dev tenant record
            await EnsureDevTenantAsync(connectionString);

            // Step 3: Create tenant schema
            await EnsureTenantSchemaAsync(connectionString);

            // Step 4: Create all module tables in tenant schema
            await EnsureIdentityTablesAsync(app.Services, connectionString);
            await EnsureModuleTablesAsync<ContactsDbContext>(app.Services, connectionString, "contacts_contacts");
            await EnsureModuleTablesAsync<DocumentsDbContext>(app.Services, connectionString, "documents_documents");
            await EnsureModuleTablesAsync<NotificationsDbContext>(app.Services, connectionString, "notifications_templates");
            await EnsureModuleTablesAsync<ReportingDbContext>(app.Services, connectionString, "reporting_report_definitions");
            await EnsureModuleTablesAsync<AuditDbContext>(app.Services, connectionString, "audit_entries");

            // Step 5: Apply incremental schema changes (new columns added after initial table creation)
            await ApplySchemaUpdatesAsync(connectionString);

            // Step 6: Seed permissions, roles, organization, tenant record
            await SeedIdentityDataAsync(app.Services, connectionString);

            // Step 7: Register all modules for the dev tenant
            await EnsureTenantModulesAsync(connectionString);

            Log.Information("[DevSeed] Development tenant provisioning complete");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[DevSeed] Failed to provision development tenant");
            throw;
        }
    }

    private static async Task EnsurePlatformTablesAsync(IServiceProvider rootSp, string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Check if platform tables already exist
        var exists = await TableExistsAsync(conn, "public", "platform_tenants");
        if (exists)
        {
            Log.Information("[DevSeed] Platform tables already exist");
            return;
        }

        // Use PlatformDbContext's model to create tables via IRelationalDatabaseCreator
        using var scope = rootSp.CreateScope();
        var platformDb = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var creator = platformDb.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();

        Log.Information("[DevSeed] Platform tables created");
    }

    private static async Task EnsureDevTenantAsync(string connectionString)
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
        Log.Information("[DevSeed] Dev tenant: {Status}", rows > 0 ? "created" : "already exists");
    }

    private static async Task EnsureTenantSchemaAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Schema name is a compile-time constant, safe for interpolation
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""CREATE SCHEMA IF NOT EXISTS "{SchemaName}" """;
        await cmd.ExecuteNonQueryAsync();

        Log.Information("[DevSeed] Tenant schema ensured: {Schema}", SchemaName);
    }

    private static async Task EnsureIdentityTablesAsync(IServiceProvider rootSp, string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Check if identity tables already exist in tenant schema
        var exists = await TableExistsAsync(conn, SchemaName, "identity_users");
        if (exists)
        {
            Log.Information("[DevSeed] Identity tables already exist in tenant schema");
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

        Log.Information("[DevSeed] Identity tables created in tenant schema");
    }

    private static async Task SeedIdentityDataAsync(IServiceProvider rootSp, string connectionString)
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
            Log.Information("[DevSeed] Seeded tenant record in identity schema");
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
            Log.Information("[DevSeed] Seeded default organization: {OrgId}", DevOrgGuid);
        }

        // Seed permissions (incremental — adds any missing permissions)
        var existingKeys = (await dbContext.Permissions.ToListAsync())
            .Select(p => $"{p.Module}.{p.Resource}.{p.Action}")
            .ToHashSet();

        var allDefaultPermissions = CreateDefaultPermissions();
        var newPermissions = allDefaultPermissions
            .Where(p => !existingKeys.Contains($"{p.Module}.{p.Resource}.{p.Action}"))
            .ToArray();

        if (newPermissions.Length > 0)
        {
            await dbContext.Permissions.AddRangeAsync(newPermissions);
            await dbContext.SaveChangesAsync();
            Log.Information("[DevSeed] Seeded {NewCount} new permissions (total: {TotalCount})",
                newPermissions.Length, existingKeys.Count + newPermissions.Length);
        }
        else if (existingKeys.Count == 0)
        {
            await dbContext.Permissions.AddRangeAsync(allDefaultPermissions);
            await dbContext.SaveChangesAsync();
            Log.Information("[DevSeed] Seeded {Count} permissions", allDefaultPermissions.Length);
        }

        // Seed Platform Admin role with all permissions (or update if new permissions added)
        var adminRole = await dbContext.Roles.Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.IsSystemRole);

        if (adminRole is null)
        {
            var tenantId = TenantId.From(DevTenantGuid);
            adminRole = Role.Create(tenantId, "Platform Admin",
                "lockey_identity_role_platform_admin_description", isSystem: true);

            var allPermissions = await dbContext.Permissions.ToListAsync();
            foreach (var permission in allPermissions)
                adminRole.AssignPermission(permission);

            await dbContext.Roles.AddAsync(adminRole);
            await dbContext.SaveChangesAsync();
            Log.Information("[DevSeed] Seeded Platform Admin role with {Count} permissions", allPermissions.Count);
        }
        else if (newPermissions.Length > 0)
        {
            // Assign newly added permissions to existing Platform Admin role
            var assignedPermissionIds = adminRole.Permissions
                .Select(rp => rp.PermissionId)
                .ToHashSet();

            var allPermissions = await dbContext.Permissions.ToListAsync();
            var unassigned = allPermissions.Where(p => !assignedPermissionIds.Contains(p.Id)).ToList();

            foreach (var permission in unassigned)
                adminRole.AssignPermission(permission);

            await dbContext.SaveChangesAsync();
            Log.Information("[DevSeed] Assigned {Count} new permissions to Platform Admin", unassigned.Count);
        }

        // Seed admin user (matches Keycloak test user)
        await SeedAdminUserAsync(dbContext, connectionString);
    }

    private static async Task SeedAdminUserAsync(IdentityDbContext dbContext, string connectionString)
    {
        // Query Keycloak for the admin user's UUID (sub claim)
        string? keycloakUserId = null;
        try
        {
            using var http = new HttpClient();

            // Get admin token from Keycloak master realm
            var tokenForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "admin-cli",
                ["username"] = "admin",
                ["password"] = "admin",
                ["grant_type"] = "password",
            });
            var tokenResp = await http.PostAsync("http://keycloak:8080/realms/master/protocol/openid-connect/token", tokenForm);
            if (!tokenResp.IsSuccessStatusCode)
            {
                Log.Warning("[DevSeed] Cannot reach Keycloak admin API — skipping admin user seed");
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
                Log.Warning("[DevSeed] Keycloak user lookup failed — skipping admin user seed");
                return;
            }

            var usersJson = await usersResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            if (usersJson.GetArrayLength() > 0)
                keycloakUserId = usersJson[0].GetProperty("id").GetString();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[DevSeed] Keycloak not reachable — skipping admin user seed");
            return;
        }

        if (string.IsNullOrEmpty(keycloakUserId))
        {
            Log.Warning("[DevSeed] Keycloak user admin@nexora.dev not found — skipping admin user seed");
            return;
        }

        // Check if user already exists
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.KeycloakUserId == keycloakUserId);

        if (existingUser is not null)
        {
            Log.Information("[DevSeed] Admin user already exists: {UserId}", existingUser.Id);
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
        Log.Information("[DevSeed] Seeded admin user {Email} (Keycloak: {KeycloakId})",
            "admin@nexora.dev", keycloakUserId);
    }

    private static async Task EnsureModuleTablesAsync<TContext>(
        IServiceProvider rootSp, string connectionString, string checkTable)
        where TContext : DbContext
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var exists = await TableExistsAsync(conn, SchemaName, checkTable);
        if (exists)
        {
            Log.Information("[DevSeed] {Module} tables already exist in tenant schema", typeof(TContext).Name);
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

        Log.Information("[DevSeed] {Module} tables created in tenant schema", typeof(TContext).Name);
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
    private static async Task ApplySchemaUpdatesAsync(string connectionString)
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
            // OrganizationUser.JoinedAt — added for member join date tracking
            "ALTER TABLE identity_organization_users ADD COLUMN IF NOT EXISTS \"JoinedAt\" timestamptz DEFAULT now()",
            // UserRole.AssignedAt — added for role assignment date tracking
            "ALTER TABLE identity_user_roles ADD COLUMN IF NOT EXISTS \"AssignedAt\" timestamptz DEFAULT now()",
        };

        foreach (var sql in alterStatements)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        Log.Information("[DevSeed] Schema updates applied ({Count} statements)", alterStatements.Length);
    }

    private static async Task EnsureTenantModulesAsync(string connectionString)
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

        Log.Information("[DevSeed] Tenant modules registered: {Modules}", string.Join(", ", moduleNames));
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

    private static Permission[] CreateDefaultPermissions() =>
    [
        // Identity
        Permission.Create("identity", "tenants", "read", "lockey_identity_permission_tenants_read"),
        Permission.Create("identity", "tenants", "create", "lockey_identity_permission_tenants_create"),
        Permission.Create("identity", "tenants", "update", "lockey_identity_permission_tenants_update"),
        Permission.Create("identity", "tenants", "delete", "lockey_identity_permission_tenants_delete"),
        Permission.Create("identity", "organizations", "read", "lockey_identity_permission_organizations_read"),
        Permission.Create("identity", "organizations", "create", "lockey_identity_permission_organizations_create"),
        Permission.Create("identity", "organizations", "update", "lockey_identity_permission_organizations_update"),
        Permission.Create("identity", "organizations", "delete", "lockey_identity_permission_organizations_delete"),
        Permission.Create("identity", "users", "read", "lockey_identity_permission_users_read"),
        Permission.Create("identity", "users", "create", "lockey_identity_permission_users_create"),
        Permission.Create("identity", "users", "update", "lockey_identity_permission_users_update"),
        Permission.Create("identity", "users", "delete", "lockey_identity_permission_users_delete"),
        Permission.Create("identity", "roles", "read", "lockey_identity_permission_roles_read"),
        Permission.Create("identity", "roles", "create", "lockey_identity_permission_roles_create"),
        Permission.Create("identity", "roles", "update", "lockey_identity_permission_roles_update"),
        Permission.Create("identity", "roles", "delete", "lockey_identity_permission_roles_delete"),
        Permission.Create("identity", "modules", "read", "lockey_identity_permission_modules_read"),
        Permission.Create("identity", "modules", "manage", "lockey_identity_permission_modules_manage"),
        // Contacts
        Permission.Create("contacts", "contact", "read", "lockey_contacts_permission_contact_read"),
        Permission.Create("contacts", "contact", "create", "lockey_contacts_permission_contact_create"),
        Permission.Create("contacts", "contact", "update", "lockey_contacts_permission_contact_update"),
        Permission.Create("contacts", "contact", "delete", "lockey_contacts_permission_contact_delete"),
        Permission.Create("contacts", "tag", "read", "lockey_contacts_permission_tag_read"),
        Permission.Create("contacts", "tag", "create", "lockey_contacts_permission_tag_create"),
        Permission.Create("contacts", "tag", "update", "lockey_contacts_permission_tag_update"),
        Permission.Create("contacts", "tag", "delete", "lockey_contacts_permission_tag_delete"),
        Permission.Create("contacts", "custom-field", "read", "lockey_contacts_permission_custom_field_read"),
        Permission.Create("contacts", "custom-field", "manage", "lockey_contacts_permission_custom_field_manage"),
        Permission.Create("contacts", "note", "create", "lockey_contacts_permission_note_create"),
        Permission.Create("contacts", "note", "update", "lockey_contacts_permission_note_update"),
        Permission.Create("contacts", "note", "read", "lockey_contacts_permission_note_read"),
        Permission.Create("contacts", "note", "delete", "lockey_contacts_permission_note_delete"),
        Permission.Create("contacts", "relationship", "create", "lockey_contacts_permission_relationship_create"),
        Permission.Create("contacts", "relationship", "delete", "lockey_contacts_permission_relationship_delete"),
        Permission.Create("contacts", "import", "execute", "lockey_contacts_permission_import_execute"),
        Permission.Create("contacts", "export", "execute", "lockey_contacts_permission_export_execute"),
        Permission.Create("contacts", "gdpr", "export", "lockey_contacts_permission_gdpr_export"),
        Permission.Create("contacts", "gdpr", "delete", "lockey_contacts_permission_gdpr_delete"),
        Permission.Create("contacts", "merge", "execute", "lockey_contacts_permission_merge_execute"),
        // Documents
        Permission.Create("documents", "document", "read", "lockey_documents_permission_document_read"),
        Permission.Create("documents", "document", "upload", "lockey_documents_permission_document_upload"),
        Permission.Create("documents", "document", "update", "lockey_documents_permission_document_update"),
        Permission.Create("documents", "document", "delete", "lockey_documents_permission_document_delete"),
        Permission.Create("documents", "folder", "read", "lockey_documents_permission_folder_read"),
        Permission.Create("documents", "folder", "manage", "lockey_documents_permission_folder_manage"),
        Permission.Create("documents", "signature", "read", "lockey_documents_permission_signature_read"),
        Permission.Create("documents", "signature", "create", "lockey_documents_permission_signature_create"),
        Permission.Create("documents", "signature", "manage", "lockey_documents_permission_signature_manage"),
        Permission.Create("documents", "template", "read", "lockey_documents_permission_template_read"),
        Permission.Create("documents", "template", "manage", "lockey_documents_permission_template_manage"),
        // Notifications
        Permission.Create("notifications", "notification", "read", "lockey_notifications_permission_notification_read"),
        Permission.Create("notifications", "notification", "send", "lockey_notifications_permission_notification_send"),
        Permission.Create("notifications", "template", "read", "lockey_notifications_permission_template_read"),
        Permission.Create("notifications", "template", "manage", "lockey_notifications_permission_template_manage"),
        Permission.Create("notifications", "provider", "read", "lockey_notifications_permission_provider_read"),
        Permission.Create("notifications", "provider", "manage", "lockey_notifications_permission_provider_manage"),
        Permission.Create("notifications", "schedule", "read", "lockey_notifications_permission_schedule_read"),
        Permission.Create("notifications", "schedule", "manage", "lockey_notifications_permission_schedule_manage"),
        // Reporting
        Permission.Create("reporting", "definition", "read", "lockey_reporting_permission_definition_read"),
        Permission.Create("reporting", "definition", "manage", "lockey_reporting_permission_definition_manage"),
        Permission.Create("reporting", "execution", "run", "lockey_reporting_permission_execution_run"),
        Permission.Create("reporting", "execution", "read", "lockey_reporting_permission_execution_read"),
        Permission.Create("reporting", "schedule", "manage", "lockey_reporting_permission_schedule_manage"),
        Permission.Create("reporting", "dashboard", "read", "lockey_reporting_permission_dashboard_read"),
        Permission.Create("reporting", "dashboard", "manage", "lockey_reporting_permission_dashboard_manage"),
        // Audit
        Permission.Create("audit", "logs", "read", "lockey_audit_permission_logs_read"),
        Permission.Create("audit", "logs", "export", "lockey_audit_permission_logs_export"),
        Permission.Create("audit", "settings", "read", "lockey_audit_permission_settings_read"),
        Permission.Create("audit", "settings", "manage", "lockey_audit_permission_settings_manage"),
    ];
}
