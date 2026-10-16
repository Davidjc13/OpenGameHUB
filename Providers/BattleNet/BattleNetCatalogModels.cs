using System.Text.Json.Serialization;

namespace OpenGameHUB.Providers.BattleNet;

internal sealed class BattleNetGamesCatalog
{
    [JsonPropertyName("Games")]
    public List<BattleNetCatalogGame> Games { get; set; } = [];
}

internal sealed class BattleNetCatalogGame
{
    [JsonPropertyName("InternalId")]
    public string InternalId { get; set; } = string.Empty;

    [JsonPropertyName("ProductId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ApiTitleId")]
    public string? ApiTitleId { get; set; }

    [JsonPropertyName("AlwaysVisible")]
    public bool AlwaysVisible { get; set; }

    [JsonPropertyName("Aliases")]
    public List<string> Aliases { get; set; } = [];
}

internal sealed record BattleNetCatalogEntry(
    string InternalId,
    string ProductId,
    string Title,
    bool Playable,
    bool Installed,
    string? InstallPath);

internal sealed record BattleNetAccountLibraryEntry(string ProductId, string Title);
