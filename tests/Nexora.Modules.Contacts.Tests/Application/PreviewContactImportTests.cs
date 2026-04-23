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

/// <summary>Unit tests for <see cref="PreviewContactImportHandler"/>.</summary>
public sealed class PreviewContactImportTests
{
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IFileStorageService _fileStorageService;
    private readonly IOptions<StorageOptions> _storageOptions;

    public PreviewContactImportTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        _fileStorageService = Substitute.For<IFileStorageService>();
        _storageOptions = Options.Create(new StorageOptions());
    }

    [Fact]
    public async Task Handle_ValidCsv_ReturnsHeadersAndFirstRows()
    {
        var csv = "FirstName,Email\nAda,ada@example.com\nGrace,grace@example.com\n";
        var bytes = Encoding.UTF8.GetBytes(csv);

        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _fileStorageService.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(bytes);

        var handler = new PreviewContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            NullLogger<PreviewContactImportHandler>.Instance);

        var result = await handler.Handle(
            new PreviewContactImportCommand($"{_orgId}/contacts/imports/abc/c.csv", "csv"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Headers.Should().BeEquivalentTo(new[] { "FirstName", "Email" });
        result.Value.Rows.Should().HaveCount(2);
        result.Value.TotalRowCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_StorageKeyWrongOrg_ReturnsFailure()
    {
        var handler = new PreviewContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            NullLogger<PreviewContactImportHandler>.Instance);

        var wrongOrg = Guid.NewGuid();
        var result = await handler.Handle(
            new PreviewContactImportCommand($"{wrongOrg}/contacts/imports/abc/c.csv", "csv"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_import_invalid_storage_key");
    }

    [Fact]
    public async Task Handle_FileMissing_ReturnsFailure()
    {
        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new PreviewContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            NullLogger<PreviewContactImportHandler>.Instance);

        var result = await handler.Handle(
            new PreviewContactImportCommand($"{_orgId}/contacts/imports/abc/c.csv", "csv"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_contacts_error_import_file_not_found");
    }

    [Fact]
    public async Task Handle_TruncatesPreviewToFiveRows()
    {
        var sb = new StringBuilder("Email\n");
        for (var i = 0; i < 10; i++) sb.Append($"u{i}@example.com\n");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());

        _fileStorageService.ObjectExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _fileStorageService.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(bytes);

        var handler = new PreviewContactImportHandler(
            _fileStorageService, _tenantAccessor, _storageOptions,
            NullLogger<PreviewContactImportHandler>.Instance);

        var result = await handler.Handle(
            new PreviewContactImportCommand($"{_orgId}/contacts/imports/abc/c.csv", "csv"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Rows.Should().HaveCount(5);
        result.Value.TotalRowCount.Should().Be(10);
    }
}
