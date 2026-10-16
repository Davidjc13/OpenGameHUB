using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetAccountJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}

internal sealed class BattleNetGamesAndSubsDocument
{
    [JsonPropertyName("gameAccounts")]
    public List<BattleNetGameAccountDocument> GameAccounts { get; set; } = [];
}

internal sealed class BattleNetGameAccountDocument
{
    [JsonPropertyName("titleId")]
    public JsonElement TitleId { get; set; }

    [JsonPropertyName("localizedGameName")]
    public string LocalizedGameName { get; set; } = string.Empty;

    public string GetTitleIdKey()
    {
        return TitleId.ValueKind switch
        {
            JsonValueKind.Number => TitleId.GetInt64().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => TitleId.GetString() ?? string.Empty,
            _ => string.Empty
        };
    }
}

internal sealed class BattleNetClassicGamesDocument
{
    [JsonPropertyName("classicGames")]
    public List<BattleNetClassicGameDocument> ClassicGames { get; set; } = [];
}

internal sealed class BattleNetClassicGameDocument
{
    [JsonPropertyName("regionalGameFranchiseIconFilename")]
    public string RegionalGameFranchiseIconFilename { get; set; } = string.Empty;
}

internal sealed class BattleNetApiStatusDocument
{
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; set; }
}
