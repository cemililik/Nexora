using Microsoft.AspNetCore.Http;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Extensions;

namespace Nexora.Infrastructure.Audit;

/// <summary>
/// Scoped service that extracts audit context from the current HTTP request.
/// </summary>
public sealed class HttpAuditContext(
    IHttpContextAccessor httpContextAccessor,
    ITenantContextAccessor tenantContextAccessor) : IAuditContext
{
    /// <inheritdoc />
    public Guid? UserId
    {
        get
        {
            var userIdString = tenantContextAccessor.Current.UserId;
            return Guid.TryParse(userIdString, out var userId) ? userId : null;
        }
    }

    /// <inheritdoc />
    // Full email stored in audit entries for compliance audit trail (not logged to Serilog)
    public string? UserEmail =>
        httpContextAccessor.HttpContext?.User.GetEmail();

    /// <inheritdoc />
    public string? IpAddress
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null) return null;

            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                // X-Forwarded-For can contain multiple IPs; take the first (originating client).
                // Validate format to prevent header-injection — only accept well-formed IP addresses.
                // Full spoofing mitigation requires configuring ForwardedHeadersMiddleware with KnownProxies.
                var firstIp = forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
                if (System.Net.IPAddress.TryParse(firstIp, out var parsedIp))
                    return parsedIp.ToString();
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }

    /// <inheritdoc />
    public string? UserAgent =>
        httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();

    /// <inheritdoc />
    public string? CorrelationId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null) return null;

            var correlationHeader = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
            return !string.IsNullOrWhiteSpace(correlationHeader)
                ? correlationHeader
                : context.TraceIdentifier;
        }
    }
}
