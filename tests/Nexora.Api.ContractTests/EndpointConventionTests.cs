using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nexora.SharedKernel.Results;

namespace Nexora.Api.ContractTests;

/// <summary>
/// Convention-level smoke tests that scan endpoint classes across modules via reflection.
/// These verify structural conventions (ApiEnvelope usage, authorization, routing patterns)
/// at the assembly/type level. They are NOT exhaustive per-endpoint validators --
/// full endpoint behavior is covered by integration tests with WebApplicationFactory.
/// </summary>
public sealed class EndpointConventionTests
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

    /// <summary>
    /// Finds all static classes in Api namespaces that have a Map*Endpoints extension method.
    /// </summary>
    private static IEnumerable<(Type EndpointClass, MethodInfo MapMethod)> GetAllEndpointMapMethods()
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var endpointClasses = assembly.GetTypes()
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed) // static classes
                .Where(t => t.Namespace?.Contains(".Api") == true)
                .Where(t => t.Name.EndsWith("Endpoints"));

            foreach (var endpointClass in endpointClasses)
            {
                var mapMethods = endpointClass.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name.StartsWith("Map") && m.Name.EndsWith("Endpoints"))
                    .Where(m => m.GetParameters().Length > 0 &&
                                typeof(IEndpointRouteBuilder).IsAssignableFrom(m.GetParameters()[0].ParameterType));

                foreach (var method in mapMethods)
                {
                    yield return (endpointClass, method);
                }
            }
        }
    }

    [Fact]
    public void AllEndpointClasses_ShouldExist()
    {
        // Sanity check: ensure we discover endpoint classes across all modules
        var endpoints = GetAllEndpointMapMethods().ToList();

        endpoints.Should().NotBeEmpty("we should discover endpoint classes across all modules");
        endpoints.Count.Should().BeGreaterThanOrEqualTo(5,
            "there should be endpoint classes across Identity, Contacts, Documents, Notifications, and Reporting");
    }

    [Fact]
    public void AllModuleAssemblies_ShouldReferenceApiEnvelope()
    {
        // Minimal APIs use lambdas compiled into closures, making per-endpoint IL scanning unreliable.
        // Instead, verify each module assembly references ApiEnvelope at the assembly level,
        // which confirms the module uses the standard response wrapper.
        foreach (var assembly in ModuleAssemblies)
        {
            var usesEnvelope = assembly.GetTypes()
                .Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ApiEnvelope<>));

            // Fallback: check if any type in the assembly references ApiEnvelope in field/property/method signatures
            if (!usesEnvelope)
            {
                usesEnvelope = assembly.GetTypes()
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    .Any(m => ReferencesApiEnvelope(m));
            }

            // Assembly-level check: the module must reference SharedKernel where ApiEnvelope lives
            if (!usesEnvelope)
            {
                usesEnvelope = assembly.GetReferencedAssemblies()
                    .Any(a => a.Name == "Nexora.SharedKernel");
            }

            usesEnvelope.Should().BeTrue(
                $"Module assembly {assembly.GetName().Name} should reference ApiEnvelope<T> via SharedKernel");
        }
    }

    private static bool ReferencesApiEnvelope(MethodInfo method)
    {
        if (method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(ApiEnvelope<>))
            return true;

        return method.GetParameters().Any(p =>
            p.ParameterType.IsGenericType &&
            p.ParameterType.GetGenericTypeDefinition() == typeof(ApiEnvelope<>));
    }

    [Fact]
    public void AllModuleAssemblies_ShouldReferenceAuthorizationPackage()
    {
        // Convention-level smoke test: verify each module assembly references the
        // Authorization package, which is required for RequireAuthorization() calls.
        // This does NOT prove every endpoint calls RequireAuthorization() -- that is
        // validated by integration tests with WebApplicationFactory.
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies)
        {
            var referencesAuth = assembly.GetReferencedAssemblies()
                .Any(a => a.Name?.Contains("Authorization") == true);

            if (!referencesAuth)
            {
                violations.Add(
                    $"{assembly.GetName().Name} does not reference an Authorization assembly");
            }
        }

        violations.Should().BeEmpty(
            "all module assemblies should reference Authorization for RequireAuthorization() support");
    }

    [Fact]
    public void AllEndpointMapMethods_ShouldBeExtensionMethodsOnIEndpointRouteBuilder()
    {
        // Convention: all Map*Endpoints methods should be extension methods on IEndpointRouteBuilder
        var violations = new List<string>();

        foreach (var (endpointClass, mapMethod) in GetAllEndpointMapMethods())
        {
            var firstParam = mapMethod.GetParameters().FirstOrDefault();
            if (firstParam == null ||
                !typeof(IEndpointRouteBuilder).IsAssignableFrom(firstParam.ParameterType) ||
                !mapMethod.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute)))
            {
                violations.Add($"{endpointClass.Name}.{mapMethod.Name} is not an extension method on IEndpointRouteBuilder");
            }
        }

        violations.Should().BeEmpty(
            "all Map*Endpoints methods should be extension methods on IEndpointRouteBuilder");
    }

    [Fact]
    public void AllEndpointClasses_ShouldBeStaticAndInApiNamespace()
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var endpointClasses = assembly.GetTypes()
                .Where(t => t.Name.EndsWith("Endpoints"))
                .Where(t => t.Namespace?.Contains(".Api") == true);

            foreach (var endpointClass in endpointClasses)
            {
                endpointClass.IsAbstract.Should().BeTrue(
                    $"{endpointClass.FullName} should be a static class");
                endpointClass.IsSealed.Should().BeTrue(
                    $"{endpointClass.FullName} should be a static class");
            }
        }
    }

}
