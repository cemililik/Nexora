using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Tests.Domain;

public sealed class TenantSettingsTests
{
    [Fact]
    public void Default_ReturnsExpectedValues()
    {
        var settings = TenantSettings.Default;

        settings.DefaultLocale.Should().Be("en-US");
        settings.DefaultCurrency.Should().Be("USD");
        settings.DefaultTimezone.Should().Be("UTC");
        settings.DefaultDocumentLanguage.Should().Be("en");
    }

    [Fact]
    public void ToJson_ThenFromJson_RoundTrips()
    {
        var original = new TenantSettings("tr-TR", "TRY", "Europe/Istanbul", "tr");

        var json = original.ToJson();
        var restored = TenantSettings.FromJson(json);

        restored.Should().Be(original);
    }

    [Fact]
    public void FromJson_NullInput_ReturnsDefault()
    {
        var settings = TenantSettings.FromJson(null);

        settings.Should().Be(TenantSettings.Default);
    }

    [Fact]
    public void FromJson_EmptyString_ReturnsDefault()
    {
        var settings = TenantSettings.FromJson("");

        settings.Should().Be(TenantSettings.Default);
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsDefault()
    {
        var settings = TenantSettings.FromJson("not-valid-json{{{");

        settings.Should().Be(TenantSettings.Default);
    }

    [Fact]
    public void FromJson_ValidJson_DeserializesCorrectly()
    {
        const string json = """{"defaultLocale":"tr-TR","defaultCurrency":"TRY","defaultTimezone":"Europe/Istanbul","defaultDocumentLanguage":"tr"}""";

        var settings = TenantSettings.FromJson(json);

        settings.DefaultLocale.Should().Be("tr-TR");
        settings.DefaultCurrency.Should().Be("TRY");
        settings.DefaultTimezone.Should().Be("Europe/Istanbul");
        settings.DefaultDocumentLanguage.Should().Be("tr");
    }

    [Fact]
    public void ToJson_ProducesCamelCaseProperties()
    {
        var settings = new TenantSettings("en-US", "USD", "UTC", "en");

        var json = settings.ToJson();

        json.Should().Contain("defaultLocale");
        json.Should().Contain("defaultCurrency");
        json.Should().Contain("defaultTimezone");
        json.Should().Contain("defaultDocumentLanguage");
    }
}
