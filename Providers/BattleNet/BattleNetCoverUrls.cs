namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetCoverUrls
{
    private const string IgdbCoverBaseUrl =
        "https://images.igdb.com/igdb/image/upload/t_cover_big/";

    public static IReadOnlyList<string> GetCoverUrls(string? platformGameId, string? title)
    {
        var game = Resolve(platformGameId, title);
        if (game is null)
            return [];

        var urls = new List<string>();
        if (!string.IsNullOrWhiteSpace(game.CoverUrl))
            urls.Add(game.CoverUrl);

        if (!string.IsNullOrWhiteSpace(game.IgdbCoverId))
            urls.Add($"{IgdbCoverBaseUrl}{game.IgdbCoverId}.jpg");

        return urls;
    }

    public static string? GetOfficialCoverUrl(string? platformGameId, string? title)
    {
        var game = Resolve(platformGameId, title);
        return string.IsNullOrWhiteSpace(game?.CoverUrl) ? null : game.CoverUrl;
    }

    private static BattleNetCatalogGame? Resolve(string? platformGameId, string? title)
    {
        var byTitle = BattleNetCatalogReader.FindByTitle(title);
        if (HasArt(byTitle))
            return byTitle;

        var byId = FindById(platformGameId);
        if (HasArt(byId))
            return byId;

        return byTitle ?? byId;
    }

    private static BattleNetCatalogGame? FindById(string? platformGameId)
    {
        if (string.IsNullOrWhiteSpace(platformGameId))
            return null;

        return BattleNetCatalogReader.FindByProductId(platformGameId)
            ?? BattleNetCatalogReader.FindByUid(platformGameId);
    }

    private static bool HasArt(BattleNetCatalogGame? game) =>
        game is not null
        && (!string.IsNullOrWhiteSpace(game.CoverUrl) || !string.IsNullOrWhiteSpace(game.IgdbCoverId));
}
