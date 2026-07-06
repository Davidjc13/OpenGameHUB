namespace OpenGameHUB.Providers.Xbox;

/// <summary>
/// Opens the Xbox app or Microsoft Store to install a Game Pass / Microsoft Store title.
/// Requires a Store Product ID (e.g. 9NTL0QDWZ4FS) or Package Family Name (PFN).
/// </summary>
internal static class XboxInstallClient
{
    public static bool IsXboxAppInstalled() => FindXboxAppExecutable() is not null;

    public static IReadOnlyList<Action> BuildInstallAttempts(string? storeProductId, string? packageFamilyName)
    {
        var attempts = new List<Action>();

        if (!string.IsNullOrWhiteSpace(storeProductId))
        {
            var productId = storeProductId.Trim();
            attempts.Add(() => StartProtocol($"msxbox://game/?productId={productId}"));
            attempts.Add(() => StartProtocol($"ms-windows-store://pdp/?ProductId={productId}"));
        }

        if (!string.IsNullOrWhiteSpace(packageFamilyName))
        {
            var pfn = packageFamilyName.Trim();
            attempts.Add(() => StartProtocol($"ms-windows-store://pdp/?PFN={pfn}"));
        }

        var xboxApp = FindXboxAppExecutable();
        if (xboxApp is not null)
            attempts.Add(() => StartXboxApp(xboxApp));

        attempts.Add(() => StartProtocol("ms-windows-store://navigatetopage/?Id=Gaming"));

        return attempts;
    }

    public static void StartInstall(string? storeProductId, string? packageFamilyName)
    {
        var errors = new List<string>();
        foreach (var attempt in BuildInstallAttempts(storeProductId, packageFamilyName))
        {
            try
            {
                attempt();
                return;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        throw new InvalidOperationException(
            errors.Count == 0
                ? Loc.T("NoLaunchMethod")
                : string.Join(" | ", errors));
    }

    private static void StartXboxApp(string xboxAppPath)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = xboxAppPath,
            UseShellExecute = true
        };

        if (System.Diagnostics.Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", xboxAppPath));
    }

    private static void StartProtocol(string url)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };

        if (System.Diagnostics.Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", url));
    }

    internal static string? FindXboxAppExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WindowsApps")
        };

        foreach (var root in candidates)
        {
            if (!Directory.Exists(root))
                continue;

            var match = Directory
                .EnumerateDirectories(root, "Microsoft.GamingApp_*", SearchOption.TopDirectoryOnly)
                .Select(path => Path.Combine(path, "XboxPcApp.exe"))
                .FirstOrDefault(File.Exists);

            if (match is not null)
                return match;
        }

        return null;
    }
}
