using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Application.Commands;
using Nexora.Modules.Contacts.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using NSubstitute;

namespace Nexora.Modules.Contacts.Tests.Application;

/// <summary>Unit tests for <see cref="ValidateContactImportHandler"/>.</summary>
public sealed class ValidateContactImportTests
{
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IFileStorageService _fileStorageService;
    private readonly IOptions<StorageOptions> _storageOptions;

    public ValidateContactImportTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        _fileStorageService = Substitute.For<IFileStorageService>();
        _storageOptions = Options.Create(new StorageOptions());

        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private ValidateContactImportHandler CreateHandler() =>
        new(_fileStorageService, _tenantAccessor, _storageOptions,
            NullLogger<ValidateContactImportHandler>.Instance);

    private string StorageKey => $"{_orgId}/contacts/imports/abc/c.csv";

    private void SetFile(string csv) =>
        _fileStorageService.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(csv));

    [Fact]
    public async Task Handle_AllValid_ReturnsZeroErrors()
    {
        SetFile("Email,Phone\nada@example.com,+1234567\ngrace@example.com,+9876543\n");
        var mapping = new Dictionary<string, string>
        {
            ["Email"] = "email",
            ["Phone"] = "phone",
        };

        var result = await CreateHandler().Handle(
            new ValidateContactImportCommand(StorageKey, "csv", mapping),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRows.Should().Be(2);
        result.Value.ErrorCount.Should().Be(0);
        result.Value.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MissingEmail_ReportsRequiredError()
    {
        SetFile("Email,Phone\n,+1234567\ngrace@example.com,+9876543\n");
        var mapping = new Dictionary<string, string>
        {
            ["Email"] = "email",
            ["Phone"] = "phone",
        };

        var result = await CreateHandler().Handle(
            new ValidateContactImportCommand(StorageKey, "csv", mapping),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ErrorCount.Should().Be(1);
        result.Value.Errors.Should().ContainSingle()
            .Which.ErrorKey.Should().Be("lockey_contacts_import_validation_email_required");
    }

    [Fact]
    public async Task Handle_InvalidEmailShape_ReportsInvalidError()
    {
        SetFile("Email\nnot-an-email\n");
        var mapping = new Dictionary<string, string> { ["Email"] = "email" };

        var result = await CreateHandler().Handle(
            new ValidateContactImportCommand(StorageKey, "csv", mapping),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ErrorCount.Should().Be(1);
        result.Value.Errors[0].ErrorKey.Should().Be("lockey_contacts_import_validation_email_invalid");
    }

    [Fact]
    public async Task Handle_MappingReferencesUnknownSourceColumn_ReportsBatchError()
    {
        SetFile("Email\nada@example.com\n");
        var mapping = new Dictionary<string, string>
        {
            ["Email"] = "email",
            ["DoesNotExist"] = "phone",
        };

        var result = await CreateHandler().Handle(
            new ValidateContactImportCommand(StorageKey, "csv", mapping),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Errors.Should().Contain(e =>
            e.ErrorKey == "lockey_contacts_import_validation_unknown_source_column"
            && e.FieldName == "DoesNotExist"
            && e.RowNumber == 0);
    }

    [Fact]
    public async Task Handle_SkippedTargetField_IgnoresColumn()
    {
        SetFile("Email,Junk\nada@example.com,ignored\n");
        var mapping = new Dictionary<string, string>
        {
            ["Email"] = "email",
            ["Junk"] = "__skip__",
        };

        var result = await CreateHandler().Handle(
            new ValidateContactImportCommand(StorageKey, "csv", mapping),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ErrorCount.Should().Be(0);
    }
}
