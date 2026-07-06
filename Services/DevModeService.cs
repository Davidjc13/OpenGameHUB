using System.Diagnostics;
using OpenGameHUB.Models;
using OpenGameHUB.Services.Ea;
using OpenGameHUB.Services.Xbox;

namespace OpenGameHUB.Services;

internal static class DevModeService
{
    public static bool IsEnabled =>
#if DEBUG
        true;
#else
        string.Equals(
            Environment.GetEnvironmentVariable("OPENGAMEHUB_DEV"),
            "1",
            StringComparison.Ordinal);
#endif

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenGameHUB");

    public static AppSettings ResetConnectionSettings(AppSettings current) =>
        new()
        {
            Language = current.Language,
            SteamApiKey = string.Empty,
            SteamId = string.Empty,
            IgdbClientId = current.IgdbClientId,
            IgdbClientSecret = current.IgdbClientSecret,
            SteamGridDbApiKey = current.SteamGridDbApiKey,
            ShowGridCovers = current.ShowGridCovers,
            DismissSteamApiKeyPrompt = false,
            DismissEaLibraryPrompt = false,
            DismissLegendaryPrompt = false,
            EpicAccountId = string.Empty,
            EpicDisplayName = string.Empty,
            XboxGamertag = string.Empty
        };

    public static void ResetPlatformConnections()
    {
        TryDisconnectEpicWithTimeout(TimeSpan.FromSeconds(8));
        XboxAccountClient.SignOut();
        EaCatalogReader.InvalidateCache();
        LegendaryClient.InvalidateExecutableCache();
    }

    public static void ClearLocalLibraryCache()
    {
        TryDeleteFile(Path.Combine(DataDirectory, "library.db"));
        TryDeleteDirectory(Path.Combine(DataDirectory, "covers"));
    }

    public static void RelaunchApp()
    {
        try
        {
            var startInfo = BuildRelaunchStartInfo();
            if (startInfo is not null)
                Process.Start(startInfo);
        }
        catch
        {
            // optional
        }

        Environment.Exit(0);
    }

    private static void TryDisconnectEpicWithTimeout(TimeSpan timeout)
    {
        var task = Task.Run(async () =>
        {
            try
            {
                if (LegendaryClient.IsAvailable())
                    await LegendaryClient.RunDisconnectAsync();
                else
                    LegendaryClient.ClearStoredCredentials();
            }
            catch
            {
                LegendaryClient.ClearStoredCredentials();
            }
        });

        try
        {
            task.Wait(timeout);
        }
        catch
        {
            LegendaryClient.ClearStoredCredentials();
        }
    }

    private static ProcessStartInfo? BuildRelaunchStartInfo()
    {
        var hostPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(hostPath))
            return null;

        var workingDirectory = AppContext.BaseDirectory;
        var dllPath = Path.Combine(workingDirectory, "OpenGameHUB.dll");
        var isDotnetHost = hostPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(hostPath), "dotnet", StringComparison.OrdinalIgnoreCase);

        if (isDotnetHost && File.Exists(dllPath))
        {
            return new ProcessStartInfo
            {
                FileName = hostPath,
                Arguments = $"\"{dllPath}\"",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            };
        }

        if (!File.Exists(hostPath))
            return null;

        return new ProcessStartInfo
        {
            FileName = hostPath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true
        };
    }

    private static void TryDeleteFile(string path)
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // optional
        }
    }
}
