using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetLibraryCache
{
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenGameHUB",
        "battlenet-library.json");

    public static bool Exists() => File.Exists(CachePath);

    public static void Save(BattleNetLibraryCacheData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(CachePath, json);
    }

    public static bool TryLoad(out BattleNetLibraryCacheData data)
    {
        data = new BattleNetLibraryCacheData();
        if (!File.Exists(CachePath))
            return false;

        try
        {
            var json = File.ReadAllText(CachePath);
            data = JsonSerializer.Deserialize<BattleNetLibraryCacheData>(json, JsonOptions)
                ?? new BattleNetLibraryCacheData();
            return data.OwnedGames.Count > 0 || data.ClassicGames.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(CachePath))
                File.Delete(CachePath);
        }
        catch
        {
            // best effort
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal sealed class BattleNetLibraryCacheData
{
    public List<BattleNetOwnedGameCache> OwnedGames { get; set; } = [];
    public List<BattleNetClassicGameCache> ClassicGames { get; set; } = [];
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class BattleNetOwnedGameCache
{
    public string TitleId { get; set; } = string.Empty;
    public string LocalizedName { get; set; } = string.Empty;
}

internal sealed class BattleNetClassicGameCache
{
    public string RegionalGameFranchiseIconFilename { get; set; } = string.Empty;
}
