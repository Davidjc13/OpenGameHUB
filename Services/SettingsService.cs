using System.Text.Json;
using OpenGameHUB.Models;

namespace OpenGameHUB.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;

    public SettingsService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenGameHUB");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public void Save(AppSettings settings)
    {
        Current = settings;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            using var document = JsonDocument.Parse(json);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();

            if (!document.RootElement.TryGetProperty(nameof(AppSettings.CoverQualityMode), out _))
            {
                settings.CoverQualityMode = document.RootElement.TryGetProperty("ShowGridCovers", out var showGridCovers)
                                                && showGridCovers.ValueKind == JsonValueKind.False
                    ? CoverQualityMode.None
                    : CoverQualityMode.Low;
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }
}
