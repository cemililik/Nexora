using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Nexora.Host;
using Nexora.Host.Endpoints;
using Nexora.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog — structured logging with OTel export
    var otlpEndpoint = builder.Configuration["Observability:Logging:OtlpEndpoint"]
        ?? "http://localhost:4317";

    builder.Host.UseSerilog((context, services, config) =>
    {
        config
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "Nexora")
            .WriteTo.Console();

        if (context.HostingEnvironment.IsDevelopment())
        {
            config.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint;
            });
        }
    });

    // OpenTelemetry — traces + metrics
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService("nexora-api",
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("Nexora.*")
            .AddOtlpExporter())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("Nexora.*")
            .AddOtlpExporter());

    // Authentication — Keycloak JWT Bearer
    var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"] ?? "http://localhost:8080";
    var keycloakPublicUrl = builder.Configuration["Keycloak:PublicUrl"] ?? keycloakBaseUrl;
    var keycloakRealm = builder.Configuration["Keycloak:Realm"] ?? "nexora-dev";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Authority = internal URL (for JWKS fetching from Docker network)
            options.Authority = $"{keycloakBaseUrl}/realms/{keycloakRealm}";
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

            options.TokenValidationParameters = new TokenValidationParameters
            {
                // ValidIssuer = public URL (matches token's iss claim from browser)
                ValidateIssuer = true,
                ValidIssuer = $"{keycloakPublicUrl}/realms/{keycloakRealm}",
                // Keycloak SPA (public) clients use the azp claim for client ID;
                // the aud claim is typically "account", not our app identifiers.
                // Audience validation is handled at the APISIX gateway layer instead.
                ValidateAudience = false,
                ValidateLifetime = true,
                NameClaimType = "preferred_username",
                RoleClaimType = "roles"
            };

            // Disable Microsoft claim type mapping to preserve original claim names
            options.MapInboundClaims = false;

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Log.Warning(context.Exception, "JWT authentication failed: {Message}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var tenantId = context.Principal?.FindFirst("tenant_id")?.Value;
                    Log.Information("JWT validated — tenant_id: {TenantId}, sub: {Sub}",
                        tenantId, context.Principal?.FindFirst("sub")?.Value);
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Log.Warning("JWT challenge — Error: {Error}, Description: {Description}",
                        context.Error, context.ErrorDescription);
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();

    // CORS — allow frontend clients in development
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(
                    "http://localhost:3001",  // nexora-admin (Vite)
                    "http://localhost:3000",  // nexora-portal (Next.js)
                    "http://localhost:5173")  // nexora-admin (fallback)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    // OpenAPI
    builder.Services.AddOpenApi();

    // Infrastructure (EF Core, Dapr, Hangfire, MediatR, etc.)
    builder.Services.AddNexoraInfrastructure(builder.Configuration);

    // Dapr
    builder.Services.AddDaprClient();

    // Global exception handler
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Module discovery & registration
    builder.Services.AddNexoraModules(builder.Configuration);

    // FluentValidation — register after modules are loaded so module validators are discovered
    builder.Services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());

    var app = builder.Build();

    // Request pipeline — exception handler must be first
    app.UseExceptionHandler();
    app.UseCors();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<TenantMiddleware>();

    // Health checks — liveness, readiness, startup
    app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
    app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
    app.MapGet("/health/startup", () => Results.Ok(new { status = "started" }));
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    // Platform endpoints (localization — not module-scoped)
    app.MapLocalizationEndpoints();

    // Module endpoints
    app.MapNexoraModuleEndpoints();

    // Hangfire dashboard — dev: open access, prod: requires admin role
    app.MapHangfireDashboard("/admin/hangfire", new DashboardOptions
    {
        Authorization = app.Environment.IsDevelopment()
            ? [new Nexora.Host.HangfireDevelopmentAuthFilter()]
            : [new Nexora.Host.HangfireAdminAuthFilter()],
        DashboardTitle = "Nexora Jobs"
    });

    // Development tenant provisioning — creates schema, tables, seed data
    await DevelopmentSeed.SeedAsync(app);

    // Module startup hooks
    await app.RunModuleStartupAsync();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
