using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OpenGameHUB.Infrastructure.Secrets;

namespace OpenGameHUB.Providers.Xbox;

internal sealed class XboxGamePassSubscriptionClient
{
    private const string LicensingRelyingParty = "http://licensing.xboxlive.com";
    private const string PublisherQueryUrl = "https://collections.mp.microsoft.com/v9.0/collections/publisherQuery";

    private static readonly string[] PcGamePassSubscriptionProductIds =
    [
        "CFQ7TTC0KGQ8", // PC Game Pass
        "CFQ7TTC0KHS0", // Xbox Game Pass Ultimate
        "CFQ7TTC0P85B"  // Xbox Game Pass Premium
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions PascalJsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    private readonly HttpClient _httpClient;
    private readonly Func<string, CancellationToken, Task<string>> _authenticateUserTokenAsync;

    public XboxGamePassSubscriptionClient(
        HttpClient? httpClient = null,
        Func<string, CancellationToken, Task<string>>? authenticateUserTokenAsync = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _authenticateUserTokenAsync = authenticateUserTokenAsync
            ?? DefaultAuthenticateUserTokenAsync;
    }

    public async Task<bool> HasActivePcGamePassAsync(CancellationToken cancellationToken = default)
    {
        var licensingXsts = await EnsureLicensingXstsTokenAsync(cancellationToken);
        if (licensingXsts is null)
            return false;

        try
        {
            return await QueryActiveSubscriptionsAsync(licensingXsts, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private async Task<XboxAuthorizationData?> EnsureLicensingXstsTokenAsync(CancellationToken cancellationToken)
    {
        var cached = XboxTokenStore.LoadLicensingXstsToken();
        if (cached is not null && cached.NotAfter > DateTime.UtcNow.AddMinutes(5))
            return cached;

        var live = XboxTokenStore.LoadLiveToken();
        if (live is null || string.IsNullOrWhiteSpace(live.AccessToken))
            return null;

        var userToken = await _authenticateUserTokenAsync(live.AccessToken, cancellationToken);
        var licensingXsts = await AuthorizeXstsAsync(userToken, LicensingRelyingParty, cancellationToken);
        XboxTokenStore.SaveLicensingXstsToken(licensingXsts);
        return licensingXsts;
    }

    private async Task<bool> QueryActiveSubscriptionsAsync(
        XboxAuthorizationData licensingXsts,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            maxPageSize = 100,
            excludeDuplicates = true,
            validityType = "All",
            productSkuIds = PcGamePassSubscriptionProductIds.Select(productId => new { productId }).ToArray()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, PublisherQueryUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, PascalJsonOptions), Encoding.UTF8, "application/json")
        };

        ApplyLicensingHeaders(request.Headers, licensingXsts);
        request.Headers.UserAgent.ParseAdd("OpenGameHUB/1.0");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return false;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("status", out var status)
                || !string.Equals(status.GetString(), "Active", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!item.TryGetProperty("productId", out var productId))
                continue;

            var product = productId.GetString();
            if (product is null)
                continue;

            if (PcGamePassSubscriptionProductIds.Contains(product, StringComparer.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void ApplyLicensingHeaders(HttpRequestHeaders headers, XboxAuthorizationData xsts)
    {
        var claim = xsts.DisplayClaims.xui.FirstOrDefault()
            ?? throw new InvalidOperationException(Loc.T("XboxNotAuthenticated"));

        headers.Add("x-xbl-contract-version", "2");
        headers.Add("Authorization", $"XBL3.0 x={claim.uhs};{xsts.Token}");
    }

    private static async Task<string> DefaultAuthenticateUserTokenAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
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

        using var authResponse = await httpClient.SendAsync(authRequestMessage, cancellationToken);
        authResponse.EnsureSuccessStatusCode();

        using var authDocument = JsonDocument.Parse(await authResponse.Content.ReadAsStringAsync(cancellationToken));
        return authDocument.RootElement.GetProperty("Token").GetString()
            ?? throw new InvalidOperationException("Missing Xbox user token.");
    }

    private async Task<XboxAuthorizationData> AuthorizeXstsAsync(
        string userToken,
        string relyingParty,
        CancellationToken cancellationToken)
    {
        var authorizeRequest = new XboxAuthorizeRequest
        {
            RelyingParty = relyingParty,
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
        return JsonSerializer.Deserialize<XboxAuthorizationData>(authorizeBody, JsonOptions)
            ?? throw new InvalidOperationException("Invalid Xbox licensing XSTS response.");
    }
}
