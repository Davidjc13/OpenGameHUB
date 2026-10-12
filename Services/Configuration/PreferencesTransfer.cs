using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Services.Configuration;

public enum PreferencesTransferFailure
{
    InvalidFile,
    NewerVersion
}

public sealed class PreferencesTransferException : Exception
{
    public PreferencesTransferException(PreferencesTransferFailure failure)
        : base(failure.ToString())
    {
        Failure = failure;
    }

    public PreferencesTransferFailure Failure { get; }
}

/// <summary>
/// Portable preferences file. API keys, account ids, and sign-in state are never written.
/// </summary>
public static class PreferencesTransfer
{
    public const string FormatId = "opengamehub-preferences";
    public const int CurrentVersion = 1;
    public const string SuggestedFileName = "OpenGameHUB-preferences.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static string Serialize(AppSettings settings)
    {
        var file = new PreferencesFile
        {
            Format = FormatId,
            Version = CurrentVersion,
            Language = settings.Language is "en" or "es" ? settings.Language : "en",
            CoverQualityMode = Enum.IsDefined(settings.CoverQualityMode)
                ? settings.CoverQualityMode
                : CoverQualityMode.Low,
            UiFontScale = Enum.IsDefined(settings.UiFontScale)
                ? settings.UiFontScale
                : UiFontScale.Normal,
            ThemeMode = Enum.IsDefined(settings.ThemeMode)
                ? settings.ThemeMode
                : ThemeMode.System,
            LibraryViewMode = Enum.IsDefined(settings.LibraryViewMode)
                ? settings.LibraryViewMode
                : LibraryViewMode.Grid,
            GridCardSize = LibraryGridMetrics.ClampPreferredCardWidth(settings.GridCardSize),
            PageSlideEnabled = settings.PageSlideEnabled
        };

        return JsonSerializer.Serialize(file, JsonOptions);
    }

    public static AppSettings Apply(AppSettings current, string json)
    {
        var file = Read(json);
        var updated = current.Clone();

        if (file.Language is not null)
            updated.Language = file.Language;

        if (file.CoverQualityMode is { } coverQuality)
            updated.CoverQualityMode = coverQuality;

        if (file.UiFontScale is { } fontScale)
            updated.UiFontScale = fontScale;

        if (file.ThemeMode is { } theme)
            updated.ThemeMode = theme;

        if (file.LibraryViewMode is { } libraryView)
            updated.LibraryViewMode = libraryView;

        if (file.GridCardSize is { } cardSize)
            updated.GridCardSize = LibraryGridMetrics.ClampPreferredCardWidth(cardSize);

        if (file.PageSlideEnabled is { } pageSlide)
            updated.PageSlideEnabled = pageSlide;

        return updated;
    }

    private static PreferencesFile Read(string json)
    {
        PreferencesFile? file;
        try
        {
            file = JsonSerializer.Deserialize<PreferencesFile>(json, JsonOptions);
        }
        catch (JsonException)
        {
            throw new PreferencesTransferException(PreferencesTransferFailure.InvalidFile);
        }

        if (file is null || !string.Equals(file.Format, FormatId, StringComparison.Ordinal))
            throw new PreferencesTransferException(PreferencesTransferFailure.InvalidFile);

        if (file.Version > CurrentVersion)
            throw new PreferencesTransferException(PreferencesTransferFailure.NewerVersion);

        if (file.Version != CurrentVersion)
            throw new PreferencesTransferException(PreferencesTransferFailure.InvalidFile);

        if (file.Language is not null && file.Language is not ("en" or "es"))
            throw new PreferencesTransferException(PreferencesTransferFailure.InvalidFile);

        if (file.GridCardSize is { } cardSize && !double.IsFinite(cardSize))
            throw new PreferencesTransferException(PreferencesTransferFailure.InvalidFile);

        return file;
    }

    private sealed class PreferencesFile
    {
        public string Format { get; set; } = string.Empty;
        public int Version { get; set; }
        public string? Language { get; set; }
        public CoverQualityMode? CoverQualityMode { get; set; }
        public UiFontScale? UiFontScale { get; set; }
        public ThemeMode? ThemeMode { get; set; }
        public LibraryViewMode? LibraryViewMode { get; set; }
        public double? GridCardSize { get; set; }
        public bool? PageSlideEnabled { get; set; }
    }
}
