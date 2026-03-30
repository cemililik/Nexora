using Nexora.Modules.Audit.Application.Services;

namespace Nexora.Modules.Audit.Tests.Api;

public sealed class DiscoverAuditableOperationsTests
{
    [Theory]
    [InlineData("Nexora.Modules.Identity.Application.Commands", "identity")]
    [InlineData("Nexora.Modules.Contacts.Application.Queries", "contacts")]
    [InlineData("Nexora.Modules.Documents.Application.Commands", "documents")]
    [InlineData("Nexora.Modules.CRM.Application.Queries", "crm")]
    public void ExtractModuleName_ValidNamespace_ReturnsLowercaseModuleName(string ns, string expected)
    {
        var result = AuditOperationDiscovery.ExtractModuleName(ns);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    [InlineData("SomeRandomNamespace", "unknown")]
    [InlineData("Nexora.Application.Commands", "unknown")]
    public void ExtractModuleName_InvalidOrMissingNamespace_ReturnsUnknown(string? ns, string expected)
    {
        var result = AuditOperationDiscovery.ExtractModuleName(ns);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("CreateContactCommand", "CreateContact")]
    [InlineData("UpdateUserCommand", "UpdateUser")]
    [InlineData("DeleteRoleCommand", "DeleteRole")]
    [InlineData("RemoveTagFromContactCommand", "RemoveTagFromContact")]
    [InlineData("ArchiveDocumentCommand", "ArchiveDocument")]
    public void ExtractCommandOperationName_WithCommandSuffix_RemovesSuffix(string className, string expected)
    {
        var result = AuditOperationDiscovery.ExtractCommandOperationName(className);

        result.Should().Be(expected);
    }

    [Fact]
    public void ExtractCommandOperationName_WithoutCommandSuffix_ReturnsOriginal()
    {
        var result = AuditOperationDiscovery.ExtractCommandOperationName("SomeHandler");

        result.Should().Be("SomeHandler");
    }

    [Theory]
    [InlineData("GetUsersQuery", "Query.GetUsers")]
    [InlineData("GetAuditSettingsQuery", "Query.GetAuditSettings")]
    [InlineData("GetContactByIdQuery", "Query.GetContactById")]
    public void ExtractQueryOperationName_WithQuerySuffix_AddsPrefixAndRemovesSuffix(string className, string expected)
    {
        var result = AuditOperationDiscovery.ExtractQueryOperationName(className);

        result.Should().Be(expected);
    }

    [Fact]
    public void ExtractQueryOperationName_WithoutQuerySuffix_AddsPrefixOnly()
    {
        var result = AuditOperationDiscovery.ExtractQueryOperationName("FetchData");

        result.Should().Be("Query.FetchData");
    }

    [Theory]
    [InlineData("CreateContact", "Create")]
    [InlineData("CreateUser", "Create")]
    [InlineData("UpdateProfile", "Update")]
    [InlineData("UpdateAuditSetting", "Update")]
    [InlineData("DeleteRole", "Delete")]
    [InlineData("DeleteUser", "Delete")]
    [InlineData("RemoveTagFromContact", "Delete")]
    [InlineData("RemoveContactAddress", "Delete")]
    public void DetermineOperationType_KnownPrefixes_ReturnsCorrectType(string operationName, string expected)
    {
        var result = AuditOperationDiscovery.DetermineOperationType(operationName);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("ArchiveDocument")]
    [InlineData("MergeContacts")]
    [InlineData("SendNotification")]
    [InlineData("ActivateModule")]
    [InlineData("RecordConsent")]
    public void DetermineOperationType_UnknownPrefix_ReturnsAction(string operationName)
    {
        var result = AuditOperationDiscovery.DetermineOperationType(operationName);

        result.Should().Be("Action");
    }

    [Fact]
    public void ExtractModuleName_NamespaceEndsWithModules_ReturnsUnknown()
    {
        // Edge case: "Modules" is the last segment, no module name after it
        var result = AuditOperationDiscovery.ExtractModuleName("Nexora.Modules");

        result.Should().Be("unknown");
    }

    [Fact]
    public void ExtractModuleName_DeepNamespace_ExtractsFirstModuleSegment()
    {
        var result = AuditOperationDiscovery.ExtractModuleName(
            "Nexora.Modules.Identity.Application.Commands.Users");

        result.Should().Be("identity");
    }
}
