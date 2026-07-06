using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenGameHUB.Models;

namespace OpenGameHUB.Services;

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
            return JsonSerializer.Deserialize<SettingsSecrets>(jsonBytes, JsonOptions) ?? new SettingsSecrets();
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

        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(secrets, JsonOptions));
        var protectedBytes = ProtectedData.Protect(jsonBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(SecretsPath, protectedBytes);
    }

    public static void Clear() => TryDelete(SecretsPath);

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
