using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Steam;

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

    public void EnrichPlaytimeFromOwned(
        IReadOnlyList<UnifiedGame> games,
        IReadOnlyList<SteamOwnedGameEntry> ownedGames)
    {
        if (ownedGames.Count == 0)
            return;

        var stats = ownedGames.ToDictionary(g => g.AppId.ToString(), g => g);

        foreach (var game in games.Where(g => g.Platform == Platform.Steam))
        {
            if (!stats.TryGetValue(game.PlatformGameId, out var steamStats))
                continue;

            game.PlaytimeMinutes = steamStats.PlaytimeMinutes;
            game.LastPlayed = steamStats.LastPlayed;
        }
    }

    public static void EnrichCatalogCoverUrls(IReadOnlyList<UnifiedGame> games)
    {
        foreach (var game in games)
        {
            if (game.Platform != Platform.Steam || !string.IsNullOrWhiteSpace(game.CoverPath))
                continue;

            if (int.TryParse(game.PlatformGameId, out var appId))
                game.CatalogCoverUrl ??= GetCoverUrl(appId);
        }
    }

    public static string GetCoverUrl(int appId) =>
        $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg";

    public async Task<SteamConnectionTestResult> TestConnectionAsync(
        string apiKey,
        string steamId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(steamId))
            return new SteamConnectionTestResult(false, Loc.T("SteamSetupMissingFields"), 0);

        if (steamId.Length != 17 || !steamId.All(char.IsDigit))
            return new SteamConnectionTestResult(false, Loc.T("SteamSetupInvalidSteamId"), 0);

        try
        {
            var url =
                $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/" +
                $"?key={Uri.EscapeDataString(apiKey.Trim())}" +
                $"&steamid={Uri.EscapeDataString(steamId.Trim())}" +
                "&include_appinfo=1&include_played_free_games=1";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return new SteamConnectionTestResult(false, Loc.T("SteamSetupInvalidApiKey"), 0);

            if (!response.IsSuccessStatusCode)
                return new SteamConnectionTestResult(false, Loc.T("SteamSetupConnectionFailed"), 0);

            var parsed = System.Text.Json.JsonSerializer.Deserialize<SteamOwnedGamesResponse>(body);
            var count = parsed?.Response?.Games?.Count ?? 0;
            return new SteamConnectionTestResult(true, null, count);
        }
        catch (Exception ex)
        {
            return new SteamConnectionTestResult(false, ex.Message, 0);
        }
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
        CatalogCoverUrl = GetCoverUrl(entry.AppId),
        LaunchSpec = LaunchSpec.Protocol($"steam://install/{entry.AppId}")
    };

    public sealed record SteamOwnedGameEntry(
        int AppId,
        string Name,
        int PlaytimeMinutes,
        DateTime? LastPlayed);

    public sealed record SteamConnectionTestResult(
        bool Success,
        string? ErrorMessage,
        int GameCount);

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
