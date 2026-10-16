using System.Text.Json;

namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetAccountLibraryParser
{
    public static void MergeIntoCache(BattleNetLibraryCacheData cache, string? gamesJson, string? classicJson)
    {
        foreach (var game in ParseOwnedGames(gamesJson))
            cache.OwnedGames.Add(game);

        foreach (var classic in ParseClassicGames(classicJson))
            cache.ClassicGames.Add(classic);
    }

    public static IReadOnlyList<BattleNetOwnedGameCache> ParseOwnedGames(string? json)
    {
        if (!TryParseDocument(json, out var root))
            return [];

        var results = new List<BattleNetOwnedGameCache>();
        foreach (var item in EnumerateGameObjects(root))
        {
            var titleId = ReadTitleId(item);
            var name = ReadString(item, "localizedGameName", "localizedName", "name", "title", "gameName");
            if (string.IsNullOrWhiteSpace(titleId) && string.IsNullOrWhiteSpace(name))
                continue;

            results.Add(new BattleNetOwnedGameCache
            {
                TitleId = titleId,
                LocalizedName = name
            });
        }

        return results;
    }

    public static IReadOnlyList<BattleNetClassicGameCache> ParseClassicGames(string? json)
    {
        if (!TryParseDocument(json, out var root))
            return [];

        var results = new List<BattleNetClassicGameCache>();
        foreach (var item in EnumerateArrayProperty(root, "classicGames", "games"))
        {
            var icon = ReadString(item, "regionalGameFranchiseIconFilename", "icon", "franchiseIcon");
            if (string.IsNullOrWhiteSpace(icon))
                continue;

            results.Add(new BattleNetClassicGameCache
            {
                RegionalGameFranchiseIconFilename = icon
            });
        }

        return results;
    }

    private static bool TryParseDocument(string? json, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        var trimmed = json.TrimStart();
        if (trimmed[0] is not ('{' or '['))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IEnumerable<JsonElement> EnumerateGameObjects(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                yield return item;
            yield break;
        }

        foreach (var name in new[] { "gameAccounts", "accountGames", "games", "titles", "ownedGames" })
        {
            foreach (var item in EnumerateArrayProperty(root, name))
                yield return item;
        }

        if (root.TryGetProperty("data", out var data))
        {
            foreach (var item in EnumerateGameObjects(data))
                yield return item;
        }
    }

    private static IEnumerable<JsonElement> EnumerateArrayProperty(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in value.EnumerateArray())
                yield return item;
        }
    }

    private static string ReadTitleId(JsonElement item)
    {
        foreach (var name in new[] { "titleId", "titleID", "id", "gameId", "productId" })
        {
            if (!item.TryGetProperty(name, out var value))
                continue;

            var text = value.ValueKind switch
            {
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.String => value.GetString(),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return string.Empty;
    }

    private static string ReadString(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return string.Empty;
    }
}
