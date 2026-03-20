namespace Nexora.Modules.Identity.Domain.ValueObjects;

/// <summary>Strongly-typed ID representing a tenant.</summary>
public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId From(Guid value) => new(value);
    public static TenantId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing an organization within a tenant.</summary>
public readonly record struct OrganizationId(Guid Value)
{
    public static OrganizationId New() => new(Guid.NewGuid());
    public static OrganizationId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a user.</summary>
public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a role.</summary>
public readonly record struct RoleId(Guid Value)
{
    public static RoleId New() => new(Guid.NewGuid());
    public static RoleId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a permission.</summary>
public readonly record struct PermissionId(Guid Value)
{
    public static PermissionId New() => new(Guid.NewGuid());
    public static PermissionId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a department.</summary>
public readonly record struct DepartmentId(Guid Value)
{
    public static DepartmentId New() => new(Guid.NewGuid());
    public static DepartmentId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing an organization-user membership.</summary>
public readonly record struct OrganizationUserId(Guid Value)
{
    public static OrganizationUserId New() => new(Guid.NewGuid());
    public static OrganizationUserId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a user-role assignment.</summary>
public readonly record struct UserRoleId(Guid Value)
{
    public static UserRoleId New() => new(Guid.NewGuid());
    public static UserRoleId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a role-permission assignment.</summary>
public readonly record struct RolePermissionId(Guid Value)
{
    public static RolePermissionId New() => new(Guid.NewGuid());
    public static RolePermissionId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing an installed module for a tenant.</summary>
public readonly record struct TenantModuleId(Guid Value)
{
    public static TenantModuleId New() => new(Guid.NewGuid());
    public static TenantModuleId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing an audit log entry.</summary>
public readonly record struct AuditLogId(Guid Value)
{
    public static AuditLogId New() => new(Guid.NewGuid());
    public static AuditLogId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
