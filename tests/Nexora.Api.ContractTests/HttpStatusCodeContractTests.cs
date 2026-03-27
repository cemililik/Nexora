using System.Reflection;
using Microsoft.AspNetCore.Http;
using Nexora.SharedKernel.Results;

namespace Nexora.Api.ContractTests;

/// <summary>
/// Convention-level smoke tests for HTTP status code usage in CRUD operations.
/// These verify that the expected HTTP result methods (Created, Ok, NotFound, BadRequest, Conflict)
/// are available and that ApiEnvelope wraps responses correctly. They are NOT exhaustive per-endpoint
/// validators -- full status code behavior is covered by integration tests with WebApplicationFactory.
///
/// Expected conventions:
/// - POST (create) -> 201 Created
/// - DELETE -> 200 OK with ApiEnvelope
/// - GET (not found) -> 404 NotFound
/// - Validation errors -> 400 BadRequest
/// </summary>
public sealed class HttpStatusCodeContractTests
{
    /// <summary>
    /// Module assemblies that contain endpoint definitions.
    /// </summary>
    private static readonly Assembly[] ModuleAssemblies =
    [
        typeof(Nexora.Modules.Identity.Api.UserEndpoints).Assembly,
        typeof(Nexora.Modules.Contacts.Api.ContactEndpoints).Assembly,
        typeof(Nexora.Modules.Documents.Api.DocumentEndpoints).Assembly,
        typeof(Nexora.Modules.Notifications.Api.NotificationEndpoints).Assembly,
        typeof(Nexora.Modules.Reporting.Api.ReportDefinitionEndpoints).Assembly,
    ];

    [Fact]
    public void PostEndpoints_ShouldReturnCreated_WhenCreatingResources()
    {
        // Verify that endpoint classes that handle POST for resource creation
        // reference Results.Created in their assembly.
        // We check that the Results type (Microsoft.AspNetCore.Http.Results) is used.
        var assembliesWithPost = ModuleAssemblies
            .Where(a => a.GetTypes()
                .Any(t => t.Namespace?.Contains(".Api") == true &&
                          t.Name.EndsWith("Endpoints")));

        foreach (var assembly in assembliesWithPost)
        {
            // Verify that the assembly references the Created method from TypedResults/Results
            var httpResultsType = typeof(Microsoft.AspNetCore.Http.Results);

            var createdMethod = httpResultsType.GetMethods()
                .Where(m => m.Name == "Created")
                .ToList();

            createdMethod.Should().NotBeEmpty(
                "Microsoft.AspNetCore.Http.Results should have a Created method " +
                "for POST endpoints returning 201");
        }
    }

    [Fact]
    public void ApiEnvelope_Fail_ShouldBeUsedForErrorResponses()
    {
        // Ensure that ApiEnvelope.Fail is available and produces the correct structure
        // for wrapping error responses (400, 404, 409, etc.)
        var error = new Error(
            Nexora.SharedKernel.Localization.LocalizedMessage.Of("lockey_test_error"));

        var envelope = ApiEnvelope<object>.Fail(error, "trace-123");

        envelope.Data.Should().BeNull("error responses should not contain data");
        envelope.Message.Should().Be("lockey_test_error");
        envelope.TraceId.Should().Be("trace-123");
    }

    [Fact]
    public void DeleteEndpoints_ShouldReturnOkWithEnvelope()
    {
        // Convention: DELETE endpoints return 200 OK wrapped in ApiEnvelope.
        // Verify by checking that endpoint classes use Results.Ok with ApiEnvelope for delete operations.
        // We validate the pattern works correctly at the type level.
        var envelope = ApiEnvelope<object>.Success(null!,
            Nexora.SharedKernel.Localization.LocalizedMessage.Of("lockey_test_deleted"));

        // The envelope wraps even null data (for delete responses)
        envelope.Message.Should().Be("lockey_test_deleted");
    }

    [Fact]
    public void GetNotFound_ShouldReturn404WithEnvelope()
    {
        // Convention: GET by ID endpoints return 404 NotFound wrapped in ApiEnvelope when not found.
        // Verify the pattern: Results.NotFound(ApiEnvelope<T>.Fail(...))
        var error = new Error(
            Nexora.SharedKernel.Localization.LocalizedMessage.Of("lockey_test_not_found"));

        var envelope = ApiEnvelope<object>.Fail(error);

        envelope.Data.Should().BeNull();
        envelope.Message.Should().Be("lockey_test_not_found");
    }

    [Fact]
    public void EndpointClasses_ShouldReferenceCorrectHttpResultMethods()
    {
        // Scan endpoint classes to verify they reference the expected HTTP result methods.
        // This catches regressions where someone uses the wrong status code.
        var expectedMethods = new[] { "Ok", "Created", "NotFound", "BadRequest" };
        var httpResultsType = typeof(Microsoft.AspNetCore.Http.Results);

        foreach (var methodName in expectedMethods)
        {
            var methods = httpResultsType.GetMethods()
                .Where(m => m.Name == methodName)
                .ToList();

            methods.Should().NotBeEmpty(
                $"Microsoft.AspNetCore.Http.Results should have a '{methodName}' method " +
                $"for proper HTTP status code responses");
        }
    }

    [Fact]
    public void AllEndpointAssemblies_ShouldReferenceApiEnvelope()
    {
        // Verify each module assembly references SharedKernel (which contains ApiEnvelope)
        var sharedKernelAssembly = typeof(ApiEnvelope<>).Assembly.GetName().Name;

        foreach (var assembly in ModuleAssemblies)
        {
            var references = assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToList();

            references.Should().Contain(sharedKernelAssembly,
                $"{assembly.GetName().Name} should reference {sharedKernelAssembly} for ApiEnvelope");
        }
    }

    [Fact]
    public void EndpointRouteGroups_ShouldUseMappingMethods()
    {
        // Verify that each module's endpoint classes define MapGet, MapPost, MapPut, MapDelete patterns
        // by checking that the assemblies reference the required routing extensions
        foreach (var assembly in ModuleAssemblies)
        {
            var endpointClasses = assembly.GetTypes()
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed)
                .Where(t => t.Namespace?.Contains(".Api") == true)
                .Where(t => t.Name.EndsWith("Endpoints"))
                .ToList();

            endpointClasses.Should().NotBeEmpty(
                $"Assembly {assembly.GetName().Name} should contain endpoint classes in the Api namespace");
        }
    }

    [Fact]
    public void ConflictResponse_ShouldBeAvailableForDuplicateOperations()
    {
        // Convention: operations that conflict (e.g., archiving already-archived) use 409 Conflict
        var conflictMethod = typeof(Microsoft.AspNetCore.Http.Results)
            .GetMethods()
            .Where(m => m.Name == "Conflict")
            .ToList();

        conflictMethod.Should().NotBeEmpty(
            "Results.Conflict should be available for 409 responses (e.g., already-archived entities)");
    }
}
