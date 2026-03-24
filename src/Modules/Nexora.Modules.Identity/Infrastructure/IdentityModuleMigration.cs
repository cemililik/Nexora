using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Infrastructure;

/// <summary>
/// Applies Identity module migrations and seeds initial data for a tenant schema.
/// </summary>
public sealed class IdentityModuleMigration(
    IServiceProvider serviceProvider,
    ILogger<IdentityModuleMigration> logger) : IModuleMigration
{
    public string ModuleName => "identity";

    /// <inheritdoc />
    public async Task MigrateAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(ExtractTenantId(schemaName));

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new IdentityDbContext(options, accessor);
        await dbContext.Database.MigrateAsync(ct);

        logger.LogInformation("Identity module migration applied for schema {SchemaName}", schemaName);
    }

    /// <inheritdoc />
    public async Task SeedAsync(string schemaName, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var tenantId = ExtractTenantId(schemaName);
        accessor.SetTenant(tenantId);

        var options = CreateDbContextOptions(scope.ServiceProvider, schemaName);
        await using var dbContext = new IdentityDbContext(options, accessor);

        // Seed default permissions if not exist
        if (!await dbContext.Permissions.AnyAsync(ct))
        {
            var defaultPermissions = new[]
            {
                Permission.Create("identity", "tenants", "read", "lockey_identity_permission_tenants_read"),
                Permission.Create("identity", "tenants", "manage", "lockey_identity_permission_tenants_manage"),
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

                // Contacts module permissions
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

                // Documents module permissions
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

                // Notifications module permissions
                Permission.Create("notifications", "notification", "read", "lockey_notifications_permission_notification_read"),
                Permission.Create("notifications", "notification", "send", "lockey_notifications_permission_notification_send"),
                Permission.Create("notifications", "template", "read", "lockey_notifications_permission_template_read"),
                Permission.Create("notifications", "template", "manage", "lockey_notifications_permission_template_manage"),
                Permission.Create("notifications", "provider", "read", "lockey_notifications_permission_provider_read"),
                Permission.Create("notifications", "provider", "manage", "lockey_notifications_permission_provider_manage"),
                Permission.Create("notifications", "schedule", "read", "lockey_notifications_permission_schedule_read"),
                Permission.Create("notifications", "schedule", "manage", "lockey_notifications_permission_schedule_manage"),

                // Reporting module permissions
                Permission.Create("reporting", "definition", "read", "lockey_reporting_permission_definition_read"),
                Permission.Create("reporting", "definition", "manage", "lockey_reporting_permission_definition_manage"),
                Permission.Create("reporting", "execution", "run", "lockey_reporting_permission_execution_run"),
                Permission.Create("reporting", "execution", "read", "lockey_reporting_permission_execution_read"),
                Permission.Create("reporting", "schedule", "manage", "lockey_reporting_permission_schedule_manage"),
                Permission.Create("reporting", "dashboard", "read", "lockey_reporting_permission_dashboard_read"),
                Permission.Create("reporting", "dashboard", "manage", "lockey_reporting_permission_dashboard_manage"),
            };

            await dbContext.Permissions.AddRangeAsync(defaultPermissions, ct);
        }

        // Seed platform-admin role if not exist
        if (!await dbContext.Roles.AnyAsync(r => r.IsSystemRole, ct))
        {
            var tenantIdTyped = Domain.ValueObjects.TenantId.Parse(tenantId);
            var adminRole = Role.Create(tenantIdTyped, "Platform Admin", "lockey_identity_role_platform_admin_description", isSystem: true);

            var allPermissions = await dbContext.Permissions.ToListAsync(ct);
            foreach (var permission in allPermissions)
                adminRole.AssignPermission(permission);

            await dbContext.Roles.AddAsync(adminRole, ct);
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Identity module seed completed for schema {SchemaName}", schemaName);
    }

    private DbContextOptions<IdentityDbContext> CreateDbContextOptions(
        IServiceProvider sp, string schemaName)
    {
        var connectionString = sp.GetRequiredService<IConfiguration>()
            .GetConnectionString("Default");

        return new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
            })
            .Options;
    }

    private static string ExtractTenantId(string schemaName) =>
        schemaName.StartsWith("tenant_") ? schemaName["tenant_".Length..] : schemaName;
}
