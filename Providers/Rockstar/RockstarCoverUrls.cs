namespace OpenGameHUB.Providers.Rockstar;

internal static class RockstarCoverUrls
{
    private const string FobBaseUrl =
        "https://media-rockstargames-com.akamaized.net/rockstargames-newsite/img/global/games/fob/1280/";

    private static readonly Dictionary<string, string> FobSlugByTitleId =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["rdr2"] = "reddeadredemption2",
            ["rdr"] = "reddeadredemption",
            ["gta5"] = "gta",
            ["gta5_gen9"] = "gta",
            ["gta3"] = "grandtheftauto3",
            ["gtavc"] = "vicecity",
            ["gtasa"] = "sanandreas",
            ["bully"] = "bully",
            ["lanoire"] = "lanoire",
            ["lanoirevr"] = "lanoire",
            ["mp3"] = "maxpayne3",
            ["gta3unreal"] = "gtatrilogy",
            ["gtavcunreal"] = "gtatrilogy",
            ["gtasaunreal"] = "gtatrilogy"
        };

    private static readonly Dictionary<string, string[]> IgdbByTitleId =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gta5"] = ["co2l7z"],
            ["gta5_gen9"] = ["co2l7z"],
            ["rdr2"] = ["co1wyy"],
            ["rdr"] = ["co8hl7"],
            ["gta4"] = ["co2l7y"],
            ["gta3"] = ["co2l7x"],
            ["gtavc"] = ["co2l7w"],
            ["gtasa"] = ["co2l7v"],
            ["bully"] = ["co1rf3"],
            ["lanoire"] = ["co1rf2"],
            ["lanoirevr"] = ["co1rf2"],
            ["mp3"] = ["co1rf1"],
            ["gta3unreal"] = ["co5s5v"],
            ["gtavcunreal"] = ["co5s5u"],
            ["gtasaunreal"] = ["co5s5t"]
        };

    public static string? GetOfficialCoverUrl(string? titleId) =>
        !string.IsNullOrWhiteSpace(titleId)
        && FobSlugByTitleId.TryGetValue(titleId, out var slug)
            ? $"{FobBaseUrl}{slug}.jpg"
            : null;

    public static string? GetCoverUrl(string? platformGameId, string? title) =>
        GetOfficialCoverUrl(RockstarCatalogReader.TryResolveTitleId(platformGameId, title))
        ?? GetOfficialCoverUrl(RockstarCatalogReader.TryResolveTitleId(null, title));

    public static IEnumerable<string> GetIgdbCoverUrls(string? platformGameId, string? title)
    {
        var titleId = RockstarCatalogReader.TryResolveTitleId(platformGameId, title);
        if (string.IsNullOrWhiteSpace(titleId)
            || !IgdbByTitleId.TryGetValue(titleId, out var imageIds))
        {
            yield break;
        }

        foreach (var url in ToIgdbUrls(imageIds))
            yield return url;
    }

    private static IEnumerable<string> ToIgdbUrls(IEnumerable<string> imageIds) =>
        imageIds.Select(id => $"https://images.igdb.com/igdb/image/upload/t_cover_big/{id}.jpg");
}
