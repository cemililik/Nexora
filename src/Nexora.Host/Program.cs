using Hangfire;
using Nexora.Host;
using Nexora.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console());

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

    var app = builder.Build();

    // Request pipeline — exception handler must be first
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<TenantMiddleware>();

    // Health check
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

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
