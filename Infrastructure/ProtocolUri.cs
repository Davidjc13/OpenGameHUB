using System.Diagnostics.CodeAnalysis;

namespace OpenGameHUB.Infrastructure;

/// <summary>
/// Builds and validates launcher protocol URLs before they are handed to
/// <c>Process.Start</c> with <c>UseShellExecute=true</c>. Catalog identifiers
/// (Steam AppId, Epic AppName, EA content IDs, Xbox PFN, …) are checked against
/// a conservative token charset so they cannot inject extra path segments,
/// query parameters, or a different scheme.
/// </summary>
internal static class ProtocolUri
{
    public const int MaxCatalogTokenLength = 256;
    public const int MaxUrlLength = 2048;

    public const string StoreGamingPage = "ms-windows-store://navigatetopage/?Id=Gaming";

    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam",
        "com.epicgames.launcher",
        "link2ea",
        "origin2",
        "origin",
        "uplay",
        "goggalaxy",
        "msxbox",
        "ms-windows-store"
    };

    public static bool IsCatalogToken([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxCatalogTokenLength)
            return false;

        if (value is "." or ".." || value.Contains("..", StringComparison.Ordinal))
            return false;

        foreach (var c in value)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
                continue;
            return false;
        }

        return true;
    }

    public static bool TrySteamInstall(int appId, [NotNullWhen(true)] out string url) =>
        TrySteamAction(appId, "install", out url);

    public static bool TrySteamStore(int appId, [NotNullWhen(true)] out string url) =>
        TrySteamAction(appId, "store", out url);

    public static bool TrySteamUninstall(int appId, [NotNullWhen(true)] out string url) =>
        TrySteamAction(appId, "uninstall", out url);

    public static bool TryEpicInstall(string? appName, [NotNullWhen(true)] out string url) =>
        TryEpicApp(appName, action: "install", silent: false, out url);

    public static bool TryEpicLaunch(string? appName, [NotNullWhen(true)] out string url) =>
        TryEpicApp(appName, action: "launch", silent: true, out url);

    public static bool TryEpicUninstall(string? appName, [NotNullWhen(true)] out string url) =>
        TryEpicApp(appName, action: "uninstall", silent: false, out url);

    public static bool TryEpicStore(string? appName, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        appName = appName?.Trim();
        if (!IsCatalogToken(appName))
            return false;

        url = $"com.epicgames.launcher://store/product/{appName}";
        return true;
    }

    public static bool TryEpicCatalogInstall(
        string? catalogNamespace,
        string? catalogItemId,
        string? appName,
        [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        catalogNamespace = catalogNamespace?.Trim();
        catalogItemId = catalogItemId?.Trim();
        appName = appName?.Trim();
        if (!IsCatalogToken(catalogNamespace)
            || !IsCatalogToken(catalogItemId)
            || !IsCatalogToken(appName))
        {
            return false;
        }

        url = $"com.epicgames.launcher://apps/{catalogNamespace}%3A{catalogItemId}%3A{appName}?action=install";
        return true;
    }

    public static bool TryUplayInstall(uint uplayId, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        if (uplayId == 0)
            return false;

        url = $"uplay://install/{uplayId}";
        return true;
    }

    public static bool TryUplayInstall(string? uplayId, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        if (!uint.TryParse(uplayId, out var parsed))
            return false;

        return TryUplayInstall(parsed, out url);
    }

    public static bool TryUplayUninstall(uint uplayId, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        if (uplayId == 0)
            return false;

        url = $"uplay://uninstall/{uplayId}";
        return true;
    }

    public static bool TryUplayUninstall(string? uplayId, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        if (!uint.TryParse(uplayId, out var parsed))
            return false;

        return TryUplayUninstall(parsed, out url);
    }

    public static bool TryGogOpenGameView(string? releaseKey, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        releaseKey = releaseKey?.Trim();
        if (!IsCatalogToken(releaseKey))
            return false;

        url = $"goggalaxy://openGameView/{releaseKey}";
        return true;
    }

    public static bool TryEaLaunch(string? contentId, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        contentId = contentId?.Trim();
        if (!IsCatalogToken(contentId))
            return false;

        url = $"link2ea://launchgame/contentids/{contentId}";
        return true;
    }

    public static bool TryOriginLaunch(string? offerId, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        offerId = offerId?.Trim();
        if (!IsCatalogToken(offerId))
            return false;

        url = $"origin2://game/launch?offerIds={Uri.EscapeDataString(offerId)}";
        return true;
    }

    public static bool TryXboxProduct(string? productId, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        productId = productId?.Trim();
        if (!IsCatalogToken(productId))
            return false;

        url = $"msxbox://game/?productId={Uri.EscapeDataString(productId)}";
        return true;
    }

    public static bool TryStoreProduct(string? productId, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        productId = productId?.Trim();
        if (!IsCatalogToken(productId))
            return false;

        url = $"ms-windows-store://pdp/?ProductId={Uri.EscapeDataString(productId)}";
        return true;
    }

    public static bool TryStorePfn(string? packageFamilyName, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        packageFamilyName = packageFamilyName?.Trim();
        if (!IsCatalogToken(packageFamilyName))
            return false;

        url = $"ms-windows-store://pdp/?PFN={Uri.EscapeDataString(packageFamilyName)}";
        return true;
    }

    public static bool IsLaunchable(string? url)
    {
        if (string.IsNullOrEmpty(url) || url.Length > MaxUrlLength)
            return false;

        if (url.Contains("..", StringComparison.Ordinal))
            return false;

        if (IsShellAppsFolder(url))
            return true;

        if (HasUnsafeCharacters(url) || HasDisallowedPercentEncoding(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return false;

        return AllowedSchemes.Contains(uri.Scheme);
    }

    public static void EnsureLaunchable(string url)
    {
        if (!IsLaunchable(url))
            throw new InvalidOperationException(Loc.T("InvalidProtocolUrl"));
    }

    private static bool TrySteamAction(int appId, string action, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        if (appId <= 0)
            return false;

        url = $"steam://{action}/{appId}";
        return true;
    }

    private static bool TryEpicApp(string? appName, string action, bool silent, [NotNullWhen(true)] out string url)
    {
        url = string.Empty;
        appName = appName?.Trim();
        if (!IsCatalogToken(appName))
            return false;

        url = silent
            ? $"com.epicgames.launcher://apps/{appName}?action={action}&silent=true"
            : $"com.epicgames.launcher://apps/{appName}?action={action}";
        return true;
    }

    private static bool IsShellAppsFolder(string url)
    {
        const string prefixBackslash = "shell:AppsFolder\\";
        const string prefixSlash = "shell:AppsFolder/";

        string rest;
        if (url.StartsWith(prefixBackslash, StringComparison.OrdinalIgnoreCase))
            rest = url[prefixBackslash.Length..];
        else if (url.StartsWith(prefixSlash, StringComparison.OrdinalIgnoreCase))
            rest = url[prefixSlash.Length..];
        else
            return false;

        var separator = rest.IndexOf('!');
        if (separator <= 0 || separator >= rest.Length - 1)
            return false;

        return IsCatalogToken(rest[..separator]) && IsCatalogToken(rest[(separator + 1)..]);
    }

    private static bool HasUnsafeCharacters(string url)
    {
        var queryStart = url.IndexOf('?');

        for (var i = 0; i < url.Length; i++)
        {
            var c = url[i];
            if (c == '&')
            {
                if (queryStart < 0 || i < queryStart)
                    return true;
                continue;
            }

            if (char.IsControl(c) || char.IsWhiteSpace(c))
                return true;

            if (c is '"' or '\'' or '|' or '^' or '<' or '>' or '`'
                or ';' or '\\' or '#' or '@' or '(' or ')' or '{' or '}' or '[' or ']')
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDisallowedPercentEncoding(string url)
    {
        for (var i = 0; i < url.Length; i++)
        {
            if (url[i] != '%')
                continue;

            if (i + 2 >= url.Length)
                return true;

            if (!url.AsSpan(i, 3).Equals("%3A", StringComparison.OrdinalIgnoreCase))
                return true;

            i += 2;
        }

        return false;
    }
}
