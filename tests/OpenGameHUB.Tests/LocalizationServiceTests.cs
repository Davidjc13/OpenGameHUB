using System.Globalization;
using OpenGameHUB.Services.Configuration;

namespace OpenGameHUB.Tests;

public sealed class LocalizationServiceTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("es", "es")]
    public void ResolveLanguage_returns_explicit_supported_values(string input, string expected)
    {
        Assert.Equal(expected, LocalizationService.ResolveLanguage(input));
    }

    [Fact]
    public void ResolveLanguage_falls_back_to_current_ui_culture()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            Assert.Equal("en", LocalizationService.ResolveLanguage(null));
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Get_returns_localized_string_for_known_key()
    {
        var service = new LocalizationService();
        service.Initialize("en");

        Assert.False(string.IsNullOrWhiteSpace(service.Get("AppName")));
    }

    [Fact]
    public void Get_with_args_formats_placeholder()
    {
        var service = new LocalizationService();
        service.Initialize("en");

        Assert.Contains("beta-1.0.0", service.Get("AppUpdateAvailableHint", "beta-1.0.0"));
    }

    [Fact]
    public void SetLanguage_raises_event_when_changed()
    {
        var service = new LocalizationService();
        service.Initialize("en");

        var raised = false;
        service.LanguageChanged += (_, _) => raised = true;
        service.SetLanguage("es");

        Assert.True(raised);
        Assert.Equal("es", service.Language);
    }
}
