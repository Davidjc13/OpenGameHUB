using System.Globalization;
using System.Resources;

namespace OpenGameHUB.Services;

public sealed class LocalizationService
{
    private static readonly ResourceManager ResourceManager = new(
        "OpenGameHUB.Resources.Strings",
        typeof(LocalizationService).Assembly);

    public event EventHandler? LanguageChanged;

    public string Language { get; private set; } = "en";

    public void Initialize(string? language)
    {
        Language = ResolveLanguage(language);
        ApplyCulture(Language);
    }

    public void SetLanguage(string language)
    {
        var normalized = language == "es" ? "es" : "en";
        if (string.Equals(Language, normalized, StringComparison.Ordinal))
            return;

        Language = normalized;
        ApplyCulture(normalized);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public string Get(string key, params object[] args)
    {
        var format = Get(key);
        if (args.Length == 0)
            return format;

        var safeArgs = args.Select(static arg =>
            arg?.ToString()?.Replace("{", "{{", StringComparison.Ordinal)
                .Replace("}", "}}", StringComparison.Ordinal) ?? string.Empty).ToArray();
        return string.Format(format, safeArgs);
    }

    public static string ResolveLanguage(string? language)
    {
        if (language is "en" or "es")
            return language;

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en";
    }

    private static void ApplyCulture(string language)
    {
        var culture = new CultureInfo(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }
}

public static class Loc
{
    private static LocalizationService? _service;

    public static LocalizationService Service => _service ??= new LocalizationService();

    public static string T(string key) => Service.Get(key);

    public static string T(string key, params object[] args) => Service.Get(key, args);
}
