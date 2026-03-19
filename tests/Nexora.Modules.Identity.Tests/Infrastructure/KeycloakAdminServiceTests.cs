using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.SharedKernel.Abstractions.Secrets;
using NSubstitute;

namespace Nexora.Modules.Identity.Tests.Infrastructure;

public sealed class KeycloakAdminServiceTests
{
    private readonly ISecretProvider _secretProvider = Substitute.For<ISecretProvider>();
    private readonly ILogger<KeycloakAdminService> _logger = Substitute.For<ILogger<KeycloakAdminService>>();
    private readonly KeycloakOptions _options = new()
    {
        BaseUrl = "http://localhost:8080",
        AdminRealm = "master",
        AdminClientId = "admin-cli"
    };

    private KeycloakAdminService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri(_options.BaseUrl) };
        _secretProvider.GetSecretAsync("nexora/keycloak/admin-password", Arg.Any<CancellationToken>())
            .Returns("test-secret");

        return new KeycloakAdminService(client, Options.Create(_options), _secretProvider, _logger);
    }

    private static FakeHttpHandler CreateHandler(HttpStatusCode statusCode, object? content = null,
        Dictionary<string, string>? responseHeaders = null)
    {
        return new FakeHttpHandler(statusCode, content, responseHeaders);
    }

    [Fact]
    public async Task CreateRealmAsync_Success_ReturnsRealmName()
    {
        var handler = CreateHandler(HttpStatusCode.Created);
        var service = CreateService(handler);

        var result = await service.CreateRealmAsync("tenant-abc", "ABC Company");

        result.Should().Be("tenant-abc");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/admin/realms");
    }

    [Fact]
    public async Task CreateRealmAsync_Conflict_ReturnsRealmNameWithoutError()
    {
        var handler = CreateHandler(HttpStatusCode.Conflict);
        var service = CreateService(handler);

        var result = await service.CreateRealmAsync("existing-realm", "Existing");

        result.Should().Be("existing-realm");
    }

    [Fact]
    public async Task CreateRealmAsync_ServerError_ThrowsHttpRequestException()
    {
        var handler = CreateHandler(HttpStatusCode.InternalServerError);
        var service = CreateService(handler);

        var act = () => service.CreateRealmAsync("bad-realm", "Bad");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateUserAsync_Success_ReturnsKeycloakUserId()
    {
        var userId = Guid.NewGuid().ToString();
        var handler = CreateHandler(HttpStatusCode.Created, responseHeaders: new()
        {
            ["Location"] = $"http://localhost:8080/admin/realms/tenant-abc/users/{userId}"
        });
        var service = CreateService(handler);

        var result = await service.CreateUserAsync("tenant-abc", "john", "john@test.com",
            "John", "Doe", "temp123");

        result.Should().Be(userId);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/admin/realms/tenant-abc/users");
    }

    [Fact]
    public async Task CreateUserAsync_RequestBody_ContainsCorrectFields()
    {
        var userId = Guid.NewGuid().ToString();
        var handler = CreateHandler(HttpStatusCode.Created, responseHeaders: new()
        {
            ["Location"] = $"http://localhost:8080/admin/realms/test/users/{userId}"
        });
        var service = CreateService(handler);

        await service.CreateUserAsync("test", "jane", "jane@test.com", "Jane", "Smith", "pass123");

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.GetProperty("username").GetString().Should().Be("jane");
        root.GetProperty("email").GetString().Should().Be("jane@test.com");
        root.GetProperty("firstName").GetString().Should().Be("Jane");
        root.GetProperty("lastName").GetString().Should().Be("Smith");
        root.GetProperty("enabled").GetBoolean().Should().BeTrue();
        root.GetProperty("credentials").GetArrayLength().Should().Be(1);
        root.GetProperty("credentials")[0].GetProperty("value").GetString().Should().Be("pass123");
        root.GetProperty("credentials")[0].GetProperty("temporary").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUserAsync_Success_SendsPutRequest()
    {
        var handler = CreateHandler(HttpStatusCode.NoContent);
        var service = CreateService(handler);
        var userId = Guid.NewGuid().ToString();

        await service.UpdateUserAsync("tenant-abc", userId, "new@test.com", "Updated", "User");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/admin/realms/tenant-abc/users/{userId}");
    }

    [Fact]
    public async Task DisableUserAsync_Success_SendsEnabledFalse()
    {
        var handler = CreateHandler(HttpStatusCode.NoContent);
        var service = CreateService(handler);
        var userId = Guid.NewGuid().ToString();

        await service.DisableUserAsync("tenant-abc", userId);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("enabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task EnableUserAsync_Success_SendsEnabledTrue()
    {
        var handler = CreateHandler(HttpStatusCode.NoContent);
        var service = CreateService(handler);
        var userId = Guid.NewGuid().ToString();

        await service.EnableUserAsync("tenant-abc", userId);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        var body = await handler.LastRequest.Content!.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task EnsureAuthenticated_ObtainsTokenFromSecretProvider()
    {
        var handler = CreateHandler(HttpStatusCode.Created);
        var service = CreateService(handler);

        await service.CreateRealmAsync("test", "Test");

        await _secretProvider.Received(1).GetSecretAsync("nexora/keycloak/admin-password", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureAuthenticated_CachesToken_DoesNotRequestTwice()
    {
        var handler = CreateHandler(HttpStatusCode.Created);
        var service = CreateService(handler);

        await service.CreateRealmAsync("test1", "Test 1");
        await service.CreateRealmAsync("test2", "Test 2");

        // Token endpoint is only hit once (first request), second request reuses cached token.
        // The secret provider should only be called once.
        await _secretProvider.Received(1).GetSecretAsync("nexora/keycloak/admin-password", Arg.Any<CancellationToken>());
    }
}

/// <summary>Test HTTP handler that returns configured responses and captures requests.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly object? _content;
    private readonly Dictionary<string, string>? _responseHeaders;
    private bool _isTokenRequest = true;

    public HttpRequestMessage? LastRequest { get; private set; }

    public FakeHttpHandler(HttpStatusCode statusCode, object? content = null,
        Dictionary<string, string>? responseHeaders = null)
    {
        _statusCode = statusCode;
        _content = content;
        _responseHeaders = responseHeaders;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;

        // First call is always the token request
        if (_isTokenRequest && request.RequestUri!.PathAndQuery.Contains("/protocol/openid-connect/token"))
        {
            _isTokenRequest = false;
            var tokenResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    access_token = "fake-admin-token",
                    expires_in = 300,
                    token_type = "Bearer"
                })
            };
            return Task.FromResult(tokenResponse);
        }

        var response = new HttpResponseMessage(_statusCode);

        if (_content is not null)
            response.Content = JsonContent.Create(_content);

        if (_responseHeaders is not null)
        {
            foreach (var (key, value) in _responseHeaders)
            {
                if (key.Equals("Location", StringComparison.OrdinalIgnoreCase))
                    response.Headers.Location = new Uri(value);
                else
                    response.Headers.TryAddWithoutValidation(key, value);
            }
        }

        return Task.FromResult(response);
    }
}
