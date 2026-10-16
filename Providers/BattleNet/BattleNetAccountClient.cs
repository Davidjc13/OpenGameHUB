using System.Text.Json;
using Avalonia.Controls;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Services.Auth;
using OpenGameHUB.Views.EmbeddedBrowser;

namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetAccountClient
{
    private const string ApiStatusUrl = "https://account.battle.net/api/";
    private const string GamesUrl = "https://account.battle.net/api/games-and-subs";
    private const string ClassicGamesUrl = "https://account.battle.net/api/classic-games";
    private const string OAuthWarmupUrl = "https://account.battle.net/oauth2/authorization/account-settings";
    private const string OverviewUrl = "https://account.battle.net/overview";
    private const string GamesPageUrl = "https://account.battle.net/games";

    public static Task RefreshLibraryCacheWithHostAsync(
        WebView2Host host,
        string? interceptedGamesJson = null,
        string? interceptedClassicJson = null) =>
        RefreshLibraryCoreAsync(host, interceptedGamesJson, interceptedClassicJson);

    public static async Task RefreshLibraryCacheAsync(string profilePath, Window ownerWindow)
    {
        await BattleNetWebViewFetcher.RunAsync(
            profilePath,
            ownerWindow,
            host => RefreshLibraryCoreAsync(host)).ConfigureAwait(true);
    }

    public static async Task<bool> IsAuthenticatedAsync(string profilePath, Window ownerWindow)
    {
        var authenticated = false;
        await BattleNetWebViewFetcher.RunAsync(
            profilePath,
            ownerWindow,
            async host =>
            {
                await host.NavigateAndWaitAsync(OAuthWarmupUrl, requireSuccess: false).ConfigureAwait(true);
                var statusJson = await host.FetchJsonWithSessionAsync(ApiStatusUrl).ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(statusJson))
                    return;

                var status = JsonSerializer.Deserialize<BattleNetApiStatusDocument>(statusJson, BattleNetAccountJson.Options);
                authenticated = status?.Authenticated == true;
            }).ConfigureAwait(true);

        return authenticated;
    }

    private static async Task RefreshLibraryCoreAsync(
        WebView2Host host,
        string? interceptedGamesJson = null,
        string? interceptedClassicJson = null)
    {
        await host.NavigateAndWaitAsync(OAuthWarmupUrl, requireSuccess: false).ConfigureAwait(true);
        await host.NavigateAndWaitAsync(OverviewUrl, requireSuccess: false).ConfigureAwait(true);
        await host.NavigateAndWaitAsync(GamesPageUrl, requireSuccess: false).ConfigureAwait(true);

        var statusJson = await FetchWithRetryAsync(host, ApiStatusUrl).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(statusJson))
        {
            var status = JsonSerializer.Deserialize<BattleNetApiStatusDocument>(statusJson, BattleNetAccountJson.Options);
            if (status?.Authenticated != true)
                throw new InvalidOperationException(Loc.T("BattleNetLibrarySyncNotAuthenticated"));
        }

        var gamesJson = FirstJson(interceptedGamesJson, await FetchWithRetryAsync(host, GamesUrl).ConfigureAwait(true));
        var classicJson = FirstJson(interceptedClassicJson, await FetchWithRetryAsync(host, ClassicGamesUrl).ConfigureAwait(true));

        var cache = new BattleNetLibraryCacheData();
        BattleNetAccountLibraryParser.MergeIntoCache(cache, gamesJson, classicJson);

        if (cache.OwnedGames.Count == 0 && cache.ClassicGames.Count == 0)
        {
            AppDiagnostics.ReportError(
                area: nameof(BattleNetAccountClient),
                operation: "RefreshLibraryCoreAsync.EmptyLibrary",
                exception: new InvalidOperationException("Battle.net API returned no owned games."),
                platform: Platform.BattleNet,
                details: $"gamesPayload={(gamesJson?.Length ?? 0)} classicPayload={(classicJson?.Length ?? 0)} preview={Preview(gamesJson)}");
            throw new InvalidOperationException(Loc.T("BattleNetLibrarySyncFailed"));
        }

        cache.SavedAt = DateTimeOffset.UtcNow;
        BattleNetLibraryCache.Save(cache);
        AppDiagnostics.ReportInfo(
            nameof(BattleNetAccountClient),
            "RefreshLibraryCoreAsync",
            Platform.BattleNet,
            $"ownedCount={cache.OwnedGames.Count} classicCount={cache.ClassicGames.Count}");
    }

    private static string? FirstJson(params string?[] candidates) =>
        candidates.FirstOrDefault(json => !string.IsNullOrWhiteSpace(json) && LooksLikeJson(json));

    private static bool LooksLikeJson(string json)
    {
        var trimmed = json.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static string Preview(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? "(empty)"
            : json.Length <= 180 ? json : json[..180];

    private static async Task<string?> FetchWithRetryAsync(WebView2Host host, string url, int attempts = 3)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(400 * attempt).ConfigureAwait(true);

            var result = await host.FetchJsonResultWithSessionAsync(url).ConfigureAwait(true);
            AppDiagnostics.ReportInfo(
                nameof(BattleNetAccountClient),
                "FetchWithRetryAsync",
                Platform.BattleNet,
                $"url={url} status={result.StatusCode} bytes={result.Body?.Length ?? 0} error={result.Error ?? "-"}");

            if (result.IsSuccess && LooksLikeJson(result.Body!))
                return result.Body;
        }

        return null;
    }
}
