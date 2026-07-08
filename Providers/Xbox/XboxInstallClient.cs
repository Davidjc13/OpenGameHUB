using System.Text.Json;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Xbox;

/// <summary>
/// Opens the Xbox app or Microsoft Store to install a Game Pass / Microsoft Store title.
/// Requires a Store Product ID (e.g. 9NTL0QDWZ4FS) or Package Family Name (PFN).
/// </summary>
internal static class XboxInstallClient
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static bool IsXboxAppInstalled() => FindXboxAppExecutable() is not null;

    /// <summary>
    /// Resolves the current Store product id (e.g. 9MWR1NC6VQ6L) from a package family name.
    /// The title-history ids are legacy numeric ids the Xbox app rejects, so we look up the
    /// real big-id via the public display catalog. Returns null on any failure.
    /// </summary>
    public static async Task<string?> ResolveStoreProductIdAsync(
        string? packageFamilyName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageFamilyName))
            return null;

        try
        {
            var (market, language) = XboxCatalogMarket.Resolve();
            var url =
                "https://displaycatalog.mp.microsoft.com/v7.0/products/lookup?" +
                $"market={Uri.EscapeDataString(market)}&languages={Uri.EscapeDataString(language)}" +
                $"&alternateId=PackageFamilyName&value={Uri.EscapeDataString(packageFamilyName.Trim())}";

            using var response = await HttpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("Products", out var products)
                || products.ValueKind != JsonValueKind.Array
                || products.GetArrayLength() == 0)
            {
                return null;
            }

            var productId = products[0].TryGetProperty("ProductId", out var idElement)
                ? idElement.GetString()
                : null;

            return string.IsNullOrWhiteSpace(productId) ? null : productId;
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(XboxInstallClient),
                operation: "ResolveStoreProductIdAsync",
                exception: ex,
                platform: OpenGameHUB.Domain.Enums.Platform.GamePass,
                details: $"packageFamilyName={packageFamilyName?.Trim()}");
            return null;
        }
    }

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
        var attempts = BuildInstallAttempts(storeProductId, packageFamilyName);
        for (var i = 0; i < attempts.Count; i++)
        {
            try
            {
                attempts[i]();
                return;
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(XboxInstallClient),
                    operation: "InstallAttempt",
                    exception: ex,
                    platform: OpenGameHUB.Domain.Enums.Platform.GamePass,
                    details: $"storeProductId={storeProductId?.Trim()} | packageFamilyName={packageFamilyName?.Trim()} | attemptIndex={i}");
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

        // ShellExecute hands protocol URIs to the packaged Store/Xbox app and often
        // returns null even on success, so we only rely on it throwing for real failures
        // (e.g. an unregistered protocol raises a Win32Exception).
        System.Diagnostics.Process.Start(psi);
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

            try
            {
                var match = Directory
                    .EnumerateDirectories(root, "Microsoft.GamingApp_*", SearchOption.TopDirectoryOnly)
                    .Select(path => Path.Combine(path, "XboxPcApp.exe"))
                    .FirstOrDefault(File.Exists);

                if (match is not null)
                    return match;
            }
            catch
            {
                // WindowsApps may be inaccessible without elevated rights.
            }
        }

        return null;
    }
}
