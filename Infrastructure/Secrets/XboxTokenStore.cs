using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenGameHUB.Infrastructure.Secrets;

internal static class XboxTokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static string TokenDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenGameHUB",
            "xbox");

    private static string LiveTokenPath => Path.Combine(TokenDirectory, "login.dat");
    private static string XstsTokenPath => Path.Combine(TokenDirectory, "xsts.dat");

    public static bool HasTokens() =>
        File.Exists(LiveTokenPath) && File.Exists(XstsTokenPath);

    public static void Clear()
    {
        TryDelete(LiveTokenPath);
        TryDelete(XstsTokenPath);
    }

    public static XboxLiveTokenData? LoadLiveToken()
    {
        return LoadEncrypted<XboxLiveTokenData>(LiveTokenPath);
    }

    public static void SaveLiveToken(XboxLiveTokenData token) =>
        SaveEncrypted(LiveTokenPath, token);

    public static XboxAuthorizationData? LoadXstsToken() =>
        LoadEncrypted<XboxAuthorizationData>(XstsTokenPath);

    public static void SaveXstsToken(XboxAuthorizationData token) =>
        SaveEncrypted(XstsTokenPath, token);

    private static T? LoadEncrypted<T>(string path)
    {
        if (!File.Exists(path))
            return default;

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var jsonBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<T>(jsonBytes, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private static void SaveEncrypted<T>(string path, T value)
    {
        Directory.CreateDirectory(TokenDirectory);
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions));
        var protectedBytes = ProtectedData.Protect(jsonBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
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
