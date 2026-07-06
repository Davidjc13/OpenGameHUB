using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Infrastructure.Secrets;

internal static class SettingsSecretsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static string SecretsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenGameHUB",
            "secrets.dat");

    public static SettingsSecrets Load()
    {
        if (!File.Exists(SecretsPath))
            return new SettingsSecrets();

        try
        {
            var protectedBytes = File.ReadAllBytes(SecretsPath);
            var jsonBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return DeserializeSecrets(jsonBytes);
        }
        catch
        {
            return new SettingsSecrets();
        }
    }

    public static void Save(SettingsSecrets secrets)
    {
        var folder = Path.GetDirectoryName(SecretsPath)!;
        Directory.CreateDirectory(folder);

        if (string.IsNullOrWhiteSpace(secrets.SteamApiKey)
            && string.IsNullOrWhiteSpace(secrets.IgdbClientSecret)
            && string.IsNullOrWhiteSpace(secrets.SteamGridDbApiKey))
        {
            TryDelete(SecretsPath);
            return;
        }

        var envelope = new SettingsSecretsEnvelope { Secrets = secrets };
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
        var protectedBytes = ProtectedData.Protect(jsonBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(SecretsPath, protectedBytes);
    }

    public static void Clear() => TryDelete(SecretsPath);

    private static SettingsSecrets DeserializeSecrets(byte[] jsonBytes)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonBytes);
            var root = document.RootElement;

            if (root.TryGetProperty("version", out var versionElement)
                && versionElement.TryGetInt32(out var version)
                && version == SettingsSecretsEnvelope.CurrentVersion
                && root.TryGetProperty("secrets", out var secretsElement))
            {
                return JsonSerializer.Deserialize<SettingsSecrets>(secretsElement.GetRawText(), JsonOptions)
                       ?? new SettingsSecrets();
            }
        }
        catch
        {
            // fall through to legacy format
        }

        // v0: DPAPI blob was a bare SettingsSecrets JSON object (no version header).
        return JsonSerializer.Deserialize<SettingsSecrets>(jsonBytes, JsonOptions) ?? new SettingsSecrets();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // optional
        }
    }
}
