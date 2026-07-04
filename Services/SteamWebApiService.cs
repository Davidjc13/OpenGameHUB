using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenGameHUB.Models;

namespace OpenGameHUB.Services;

public sealed class SteamWebApiService
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task EnrichPlaytimeAsync(
        IReadOnlyList<UnifiedGame> games,
        string apiKey,
        string steamId,
        CancellationToken cancellationToken = default)
    {
        var owned = await GetOwnedGamesAsync(apiKey, steamId, cancellationToken);
        if (owned.Count == 0)
            return;

        var stats = owned.ToDictionary(g => g.AppId.ToString(), g => g);

        foreach (var game in games.Where(g => g.Platform == Platform.Steam))
        {
            if (!stats.TryGetValue(game.PlatformGameId, out var steamStats))
                continue;

            game.PlaytimeMinutes = steamStats.PlaytimeMinutes;
            game.LastPlayed = steamStats.LastPlayed;
        }
    }

    public IReadOnlyList<UnifiedGame> GetUninstalledOwnedGames(
        IReadOnlyList<SteamOwnedGameEntry> ownedGames,
        IReadOnlyList<UnifiedGame> installedGames)
    {
        var installedIds = installedGames
            .Where(g => g.Platform == Platform.Steam)
            .Select(g => g.PlatformGameId)
            .ToHashSet(StringComparer.Ordinal);

        return ownedGames
            .Where(g => !installedIds.Contains(g.AppId.ToString()))
            .Select(MapUninstalledGame)
            .Where(g => !GameEntryFilter.IsExcluded(g))
            .ToList();
    }

    public async Task<IReadOnlyList<SteamOwnedGameEntry>> GetOwnedGamesAsync(
        string apiKey,
        string steamId,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/" +
            $"?key={Uri.EscapeDataString(apiKey)}" +
            $"&steamid={Uri.EscapeDataString(steamId)}" +
            "&include_appinfo=1&include_played_free_games=1";

        var response = await _httpClient.GetFromJsonAsync<SteamOwnedGamesResponse>(url, cancellationToken);
        if (response?.Response?.Games is null)
            return [];

        return response.Response.Games
            .Where(g => g.AppId > 0 && !string.IsNullOrWhiteSpace(g.Name))
            .Select(g => new SteamOwnedGameEntry(
                g.AppId,
                g.Name.Trim(),
                g.PlaytimeForever,
                g.RtimeLastPlayed > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(g.RtimeLastPlayed).UtcDateTime
                    : null))
            .ToList();
    }

    private static UnifiedGame MapUninstalledGame(SteamOwnedGameEntry entry) => new()
    {
        Id = $"steam:store:{entry.AppId}",
        Platform = Platform.Steam,
        PlatformGameId = entry.AppId.ToString(),
        Title = entry.Name,
        IsInstalled = false,
        PlaytimeMinutes = entry.PlaytimeMinutes,
        LastPlayed = entry.LastPlayed,
        LaunchSpec = LaunchSpec.Protocol($"steam://install/{entry.AppId}")
    };

    public sealed record SteamOwnedGameEntry(
        int AppId,
        string Name,
        int PlaytimeMinutes,
        DateTime? LastPlayed);

    private sealed class SteamOwnedGamesResponse
    {
        [JsonPropertyName("response")]
        public SteamOwnedGamesBody? Response { get; set; }
    }

    private sealed class SteamOwnedGamesBody
    {
        [JsonPropertyName("games")]
        public List<SteamOwnedGame>? Games { get; set; }
    }

    private sealed class SteamOwnedGame
    {
        [JsonPropertyName("appid")]
        public int AppId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("playtime_forever")]
        public int PlaytimeForever { get; set; }

        [JsonPropertyName("rtime_last_played")]
        public long RtimeLastPlayed { get; set; }
    }
}
