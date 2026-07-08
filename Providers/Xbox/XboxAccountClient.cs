using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Xbox;

internal sealed class XboxAccountClient
{
    private const string ClientId = "38cd2fa8-66fd-4760-afb2-405eb65d5b0c";
    private const string RedirectUri = "https://login.live.com/oauth20_desktop.srf";
    private const string Scope = "Xboxlive.signin Xboxlive.offline_access";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions PascalJsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static bool IsAuthenticated() => XboxTokenStore.HasTokens();

    public static XboxOAuthSession CreateOAuthSession()
    {
        var state = GenerateUrlSafeToken(32);
        var codeVerifier = GenerateUrlSafeToken(64);
        var codeChallenge = ComputeCodeChallenge(codeVerifier);

        var url = "https://login.live.com/oauth20_authorize.srf?" + BuildQuery(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scope,
            ["approval_prompt"] = "auto",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        });

        return new XboxOAuthSession
        {
            AuthorizeUrl = url,
            State = state,
            CodeVerifier = codeVerifier
        };
    }

    public async Task CompleteLoginAsync(
        string authorizationCode,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        var oauth = await ExchangeAuthorizationCodeAsync(authorizationCode, codeVerifier, cancellationToken);
        var liveToken = new XboxLiveTokenData
        {
            AccessToken = oauth.access_token,
            RefreshToken = oauth.refresh_token,
            ExpiresIn = oauth.expires_in,
            CreationDate = DateTime.UtcNow,
            TokenType = oauth.token_type,
            UserId = oauth.user_id
        };

        XboxTokenStore.SaveLiveToken(liveToken);
        await AuthenticateXboxAsync(liveToken.AccessToken, cancellationToken);
    }

    public async Task<string?> GetGamertagAsync(CancellationToken cancellationToken = default)
    {
        var xsts = await EnsureValidXstsTokenAsync(cancellationToken);
        if (xsts?.DisplayClaims.xui.FirstOrDefault()?.gtg is { Length: > 0 } gamertag)
            return gamertag;

        return null;
    }

    public async Task<IReadOnlyList<XboxCatalogEntry>> GetPcLibraryEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        var xsts = await EnsureValidXstsTokenAsync(cancellationToken)
            ?? throw new InvalidOperationException(Loc.T("XboxNotAuthenticated"));

        var titles = await GetLibraryTitlesAsync(xsts, cancellationToken);
        var pcTitles = titles
            .Where(IsPcGameTitle)
            .Select(NormalizeCatalogEntry)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Pfn))
            .ToList();

        if (pcTitles.Count == 0)
            return pcTitles;

        try
        {
            var stats = await GetMinutesPlayedAsync(xsts, pcTitles.Select(t => t.TitleId), cancellationToken);
            return pcTitles
                .Select(entry => stats.TryGetValue(entry.TitleId, out var minutes)
                    ? entry with { PlaytimeMinutes = minutes }
                    : entry)
                .ToList();
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(XboxAccountClient),
                operation: "GetPcLibraryEntriesAsync.EnrichPlaytime",
                exception: ex,
                platform: Platform.GamePass);
            return pcTitles;
        }
    }

    public static void SignOut() => XboxTokenStore.Clear();

    private async Task<XboxOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
        string authorizationCode,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(BuildFormValues(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scope,
            ["code_verifier"] = codeVerifier
        }));

        using var response = await _httpClient.PostAsync(
            "https://login.live.com/oauth20_token.srf",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<XboxOAuthTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Invalid Xbox OAuth response.");
    }

    private async Task AuthenticateXboxAsync(string accessToken, CancellationToken cancellationToken)
    {
        var authRequest = new XboxAuthenticateRequest
        {
            Properties = new XboxAuthenticateRequestProperties
            {
                RpsTicket = $"d={accessToken}"
            }
        };

        using var authContent = new StringContent(
            JsonSerializer.Serialize(authRequest, PascalJsonOptions),
            Encoding.UTF8,
            "application/json");

        using var authRequestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            "https://user.auth.xboxlive.com/user/authenticate")
        {
            Content = authContent
        };
        authRequestMessage.Headers.Add("x-xbl-contract-version", "1");

        using var authResponse = await _httpClient.SendAsync(authRequestMessage, cancellationToken);
        authResponse.EnsureSuccessStatusCode();
        var authBody = await authResponse.Content.ReadAsStringAsync(cancellationToken);
        using var authDocument = JsonDocument.Parse(authBody);
        var userToken = authDocument.RootElement.GetProperty("Token").GetString()
            ?? throw new InvalidOperationException("Missing Xbox user token.");

        var authorizeRequest = new XboxAuthorizeRequest
        {
            Properties = new XboxAuthorizeRequestProperties
            {
                UserTokens = [userToken]
            }
        };

        using var authorizeContent = new StringContent(
            JsonSerializer.Serialize(authorizeRequest, PascalJsonOptions),
            Encoding.UTF8,
            "application/json");

        using var authorizeRequestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            "https://xsts.auth.xboxlive.com/xsts/authorize")
        {
            Content = authorizeContent
        };
        authorizeRequestMessage.Headers.Add("x-xbl-contract-version", "1");

        using var authorizeResponse = await _httpClient.SendAsync(authorizeRequestMessage, cancellationToken);
        authorizeResponse.EnsureSuccessStatusCode();
        var authorizeBody = await authorizeResponse.Content.ReadAsStringAsync(cancellationToken);
        var xsts = JsonSerializer.Deserialize<XboxAuthorizationData>(authorizeBody, JsonOptions)
            ?? throw new InvalidOperationException("Invalid Xbox XSTS response.");

        XboxTokenStore.SaveXstsToken(xsts);
    }

    private async Task<XboxAuthorizationData?> EnsureValidXstsTokenAsync(CancellationToken cancellationToken)
    {
        var xsts = XboxTokenStore.LoadXstsToken();
        if (xsts is null)
            return null;

        if (await IsLoggedInAsync(xsts, cancellationToken))
            return xsts;

        var live = XboxTokenStore.LoadLiveToken();
        if (live is null || string.IsNullOrWhiteSpace(live.RefreshToken))
            return null;

        await RefreshLiveTokenAsync(live, cancellationToken);
        live = XboxTokenStore.LoadLiveToken();
        if (live is null)
            return null;

        await AuthenticateXboxAsync(live.AccessToken, cancellationToken);
        return XboxTokenStore.LoadXstsToken();
    }

    private async Task<bool> IsLoggedInAsync(XboxAuthorizationData xsts, CancellationToken cancellationToken)
    {
        try
        {
            var xuid = xsts.DisplayClaims.xui.FirstOrDefault()?.xid;
            if (string.IsNullOrWhiteSpace(xuid) || !ulong.TryParse(xuid, out var userId))
                return false;

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://profile.xboxlive.com/users/batch/profile/settings")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new XboxProfileRequest
                    {
                        userIds = [userId]
                    }),
                    Encoding.UTF8,
                    "application/json")
            };

            ApplyXstsHeaders(request.Headers, xsts);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(XboxAccountClient),
                operation: "IsLoggedInAsync",
                exception: ex,
                platform: Platform.GamePass);
            return false;
        }
    }

    private async Task RefreshLiveTokenAsync(XboxLiveTokenData liveToken, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(BuildFormValues(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = liveToken.RefreshToken,
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scope
        }));

        using var response = await _httpClient.PostAsync(
            "https://login.live.com/oauth20_token.srf",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var oauth = JsonSerializer.Deserialize<XboxOAuthTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Invalid Xbox refresh response.");

        XboxTokenStore.SaveLiveToken(new XboxLiveTokenData
        {
            AccessToken = oauth.access_token,
            RefreshToken = oauth.refresh_token,
            ExpiresIn = oauth.expires_in,
            CreationDate = DateTime.UtcNow,
            TokenType = oauth.token_type,
            UserId = oauth.user_id
        });
    }

    private async Task<List<XboxTitleEntry>> GetLibraryTitlesAsync(
        XboxAuthorizationData xsts,
        CancellationToken cancellationToken)
    {
        var xuid = xsts.DisplayClaims.xui.FirstOrDefault()?.xid
            ?? throw new InvalidOperationException(Loc.T("XboxNotAuthenticated"));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://titlehub.xboxlive.com/users/xuid({xuid})/titles/titlehistory/decoration/detail");

        ApplyXstsHeaders(request.Headers, xsts);
        request.Headers.Add("Accept-Language", "en-US");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(Loc.T("XboxNotAuthenticated"));

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var history = JsonSerializer.Deserialize<XboxTitleHistoryResponse>(body, JsonOptions);
        return history?.titles ?? [];
    }

    private async Task<Dictionary<string, int>> GetMinutesPlayedAsync(
        XboxAuthorizationData xsts,
        IEnumerable<string> titleIds,
        CancellationToken cancellationToken)
    {
        var xuid = xsts.DisplayClaims.xui.FirstOrDefault()?.xid
            ?? throw new InvalidOperationException(Loc.T("XboxNotAuthenticated"));

        var payload = new
        {
            arrangebyfield = "xuid",
            stats = titleIds.Select(titleId => new { name = "MinutesPlayed", titleid = titleId }).ToList(),
            xuids = new[] { xuid }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://userstats.xboxlive.com/batch")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        ApplyXstsHeaders(request.Headers, xsts);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var output = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!document.RootElement.TryGetProperty("statlistscollection", out var collections)
            || collections.ValueKind != JsonValueKind.Array)
        {
            return output;
        }

        foreach (var collection in collections.EnumerateArray())
        {
            if (!collection.TryGetProperty("stats", out var stats) || stats.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var stat in stats.EnumerateArray())
            {
                var titleId = stat.GetProperty("titleid").GetString();
                var value = stat.GetProperty("value").GetString();
                if (string.IsNullOrWhiteSpace(titleId) || !int.TryParse(value, out var minutes))
                    continue;

                output[titleId] = minutes;
            }
        }

        return output;
    }

    private static void ApplyXstsHeaders(HttpRequestHeaders headers, XboxAuthorizationData xsts)
    {
        var claim = xsts.DisplayClaims.xui.FirstOrDefault();
        if (claim is null)
            throw new InvalidOperationException(Loc.T("XboxNotAuthenticated"));

        headers.Add("x-xbl-contract-version", "2");
        headers.Add("Authorization", $"XBL3.0 x={claim.uhs};{xsts.Token}");
    }

    private static bool IsPcGameTitle(XboxTitleEntry title) =>
        string.Equals(title.type, "Game", StringComparison.OrdinalIgnoreCase)
        && title.devices.Any(device => device.Equals("PC", StringComparison.OrdinalIgnoreCase))
        && !string.IsNullOrWhiteSpace(title.pfn);

    private static XboxCatalogEntry NormalizeCatalogEntry(XboxTitleEntry title)
    {
        var displayName = title.name
            .Replace("(PC)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("(Windows)", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("for Windows 10", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("- Windows 10", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        var storeProductId = FirstNonEmpty(title.windowsPhoneProductId, title.modernTitleId);
        return new XboxCatalogEntry
        {
            Pfn = title.pfn.Trim(),
            Title = displayName,
            StoreProductId = storeProductId,
            TitleId = title.titleId,
            PlaytimeMinutes = int.TryParse(title.minutesPlayed, out var minutes) ? minutes : null,
            LastPlayed = title.titleHistory?.lastTimePlayed
        };
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string BuildQuery(IReadOnlyDictionary<string, string> values) =>
        string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

    private static IEnumerable<KeyValuePair<string, string>> BuildFormValues(
        IReadOnlyDictionary<string, string> values) =>
        values.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value));

    private static string GenerateUrlSafeToken(int byteCount)
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(byteCount);
        return Base64UrlEncode(bytes);
    }

    internal static string ComputeCodeChallenge(string codeVerifier)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
