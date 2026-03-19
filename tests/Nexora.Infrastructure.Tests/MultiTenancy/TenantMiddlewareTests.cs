using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Nexora.Infrastructure.MultiTenancy;
using NSubstitute;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Tests.MultiTenancy;

public sealed class TenantMiddlewareTests
{
    private readonly ITenantContextAccessor _accessor = Substitute.For<ITenantContextAccessor>();

    [Fact]
    public async Task Invoke_WithTenantClaim_ShouldSetTenantContext()
    {
        var tenantId = Guid.NewGuid().ToString();
        var orgId = Guid.NewGuid().ToString();
        var userId = "user-1";

        var context = CreateHttpContext("/api/v1/identity/users", authenticated: true,
            claims: [
                new Claim("tenant_id", tenantId),
                new Claim("organization_id", orgId),
                new Claim(ClaimTypes.NameIdentifier, userId)
            ]);

        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _accessor);

        nextCalled.Should().BeTrue();
        _accessor.Received(1).SetTenant(tenantId, orgId, userId);
    }

    [Fact]
    public async Task Invoke_AuthenticatedWithoutTenantClaim_ShouldReturn401()
    {
        var context = CreateHttpContext("/api/v1/identity/users", authenticated: true);

        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _accessor);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        body.GetProperty("message").GetString().Should().Be("lockey_error_tenant_context_missing");
    }

    [Fact]
    public async Task Invoke_Unauthenticated_ShouldCallNextWithoutSettingTenant()
    {
        var context = CreateHttpContext("/api/v1/public/info", authenticated: false);

        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _accessor);

        nextCalled.Should().BeTrue();
        _accessor.DidNotReceive().SetTenant(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/admin/hangfire")]
    [InlineData("/admin/hangfire/jobs")]
    public async Task Invoke_PublicPath_ShouldSkipTenantResolution(string path)
    {
        var context = CreateHttpContext(path, authenticated: true);

        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _accessor);

        nextCalled.Should().BeTrue();
        _accessor.DidNotReceive().SetTenant(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Invoke_PublicPath_CaseInsensitive_ShouldSkip()
    {
        var context = CreateHttpContext("/Health", authenticated: true);

        var nextCalled = false;
        var middleware = new TenantMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _accessor);

        nextCalled.Should().BeTrue();
        _accessor.DidNotReceive().SetTenant(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Invoke_WithTenantClaim_NoOrgOrUser_ShouldSetTenantOnly()
    {
        var tenantId = Guid.NewGuid().ToString();
        var context = CreateHttpContext("/api/v1/test", authenticated: true,
            claims: [new Claim("tenant_id", tenantId)]);

        var middleware = new TenantMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, _accessor);

        _accessor.Received(1).SetTenant(tenantId, null, null);
    }

    private static DefaultHttpContext CreateHttpContext(
        string path,
        bool authenticated,
        Claim[]? claims = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (authenticated)
        {
            var claimsList = claims?.ToList() ?? [];
            var identity = new ClaimsIdentity(claimsList, "TestAuth");
            context.User = new ClaimsPrincipal(identity);
        }

        return context;
    }
}
