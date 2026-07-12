using System.Text.Json;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Infrastructure.Secrets;

namespace OpenGameHUB.Services.Configuration;

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
        SettingsSecretsStore.Save(SettingsSecrets.From(settings));
        var document = AppSettingsDocument.From(settings);
        var json = JsonSerializer.Serialize(document, JsonOptions);
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
            var root = document.RootElement;

            var persisted = JsonSerializer.Deserialize<AppSettingsDocument>(json, JsonOptions)
                            ?? new AppSettingsDocument();

            if (!root.TryGetProperty(nameof(AppSettings.CoverQualityMode), out _))
            {
                persisted.CoverQualityMode = root.TryGetProperty("ShowGridCovers", out var showGridCovers)
                                             && showGridCovers.ValueKind == JsonValueKind.False
                    ? CoverQualityMode.None
                    : CoverQualityMode.Low;
            }

            var secrets = SettingsSecretsStore.Load();
            var migratedFromPlaintext = MigratePlaintextSecrets(root, secrets);

            var settings = persisted.ToSettings(secrets);
            if (migratedFromPlaintext)
                Save(settings);

            return settings;
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(SettingsService),
                operation: "Load",
                exception: ex,
                details: "settings.json");
            return new AppSettings();
        }
    }

    private static bool MigratePlaintextSecrets(JsonElement root, SettingsSecrets secrets)
    {
        var migrated = false;

        if (string.IsNullOrWhiteSpace(secrets.SteamApiKey)
            && TryReadLegacyString(root, nameof(AppSettings.SteamApiKey), out var steamApiKey))
        {
            secrets.SteamApiKey = steamApiKey;
            migrated = true;
        }

        if (string.IsNullOrWhiteSpace(secrets.IgdbClientSecret)
            && TryReadLegacyString(root, nameof(AppSettings.IgdbClientSecret), out var igdbClientSecret))
        {
            secrets.IgdbClientSecret = igdbClientSecret;
            migrated = true;
        }

        if (string.IsNullOrWhiteSpace(secrets.SteamGridDbApiKey)
            && TryReadLegacyString(root, nameof(AppSettings.SteamGridDbApiKey), out var steamGridDbApiKey))
        {
            secrets.SteamGridDbApiKey = steamGridDbApiKey;
            migrated = true;
        }

        if (migrated)
            SettingsSecretsStore.Save(secrets);

        return migrated;
    }

    private static bool TryReadLegacyString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
