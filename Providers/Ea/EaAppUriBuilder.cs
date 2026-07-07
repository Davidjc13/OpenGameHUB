using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Ea;

internal static class EaAppUriBuilder
{
    public static string BuildOpenLibraryUrl(string slug) =>
        $"link2ea://openlibrary?slug={Uri.EscapeDataString(slug.Trim())}&platform=EA";

    public static bool TryBuildOpenLibraryUrl(UnifiedGame game, out string url)
    {
        url = string.Empty;

        string? slug = null;
        if (TryGetCatalogSlug(game.Id, out var catalogSlug))
            slug = catalogSlug;
        else if (IsGameSlug(game.PlatformGameId))
            slug = game.PlatformGameId;

        if (!IsGameSlug(slug))
            return false;

        url = BuildOpenLibraryUrl(slug!);
        return true;
    }

    public static bool TryGetCatalogSlug(string id, out string slug)
    {
        slug = string.Empty;
        const string prefix = "ea:catalog:";
        if (!id.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var payload = id[prefix.Length..];
        var separator = payload.IndexOf('@');
        if (separator <= 0 || separator >= payload.Length - 1)
            return false;

        slug = payload[(separator + 1)..];
        return IsGameSlug(slug);
    }

    private static bool IsGameSlug(string? value) =>
        EaCatalogReader.IsValidGameSlug(value);
}
