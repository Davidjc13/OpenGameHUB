using System.Text.Json.Serialization;

namespace OpenGameHUB.Providers.Xbox;

internal sealed class XboxLiveTokenData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public DateTime CreationDate { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
}

internal sealed class XboxAuthorizationData
{
    public string Token { get; set; } = string.Empty;
    public DateTime IssueInstant { get; set; }
    public DateTime NotAfter { get; set; }
    public XboxDisplayClaims DisplayClaims { get; set; } = new();
}

internal sealed class XboxDisplayClaims
{
    public List<XboxXuiClaim> xui { get; set; } = [];
}

internal sealed class XboxXuiClaim
{
    public string uhs { get; set; } = string.Empty;
    public string xid { get; set; } = string.Empty;
    public string gtg { get; set; } = string.Empty;
}

internal sealed class XboxOAuthTokenResponse
{
    public string access_token { get; set; } = string.Empty;
    public string refresh_token { get; set; } = string.Empty;
    public int expires_in { get; set; }
    public string token_type { get; set; } = string.Empty;
    public string user_id { get; set; } = string.Empty;
}

internal sealed class XboxTitleHistoryResponse
{
    public string xuid { get; set; } = string.Empty;
    public List<XboxTitleEntry> titles { get; set; } = [];
}

internal sealed class XboxTitleEntry
{
    public string titleId { get; set; } = string.Empty;
    public string pfn { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string windowsPhoneProductId { get; set; } = string.Empty;
    public string modernTitleId { get; set; } = string.Empty;
    public string mediaItemType { get; set; } = string.Empty;
    public List<string> devices { get; set; } = [];
    public XboxTitleDetail? detail { get; set; }
    public XboxTitleHistoryInfo? titleHistory { get; set; }
    public string minutesPlayed { get; set; } = string.Empty;
}

internal sealed class XboxTitleDetail
{
    public string description { get; set; } = string.Empty;
    public string publisherName { get; set; } = string.Empty;
    public string developerName { get; set; } = string.Empty;
    public DateTime? releaseDate { get; set; }
}

internal sealed class XboxTitleHistoryInfo
{
    public DateTime? lastTimePlayed { get; set; }
}

internal sealed class XboxAuthenticateRequest
{
    public string RelyingParty { get; set; } = "http://auth.xboxlive.com";
    public string TokenType { get; set; } = "JWT";

    [JsonPropertyName("Properties")]
    public XboxAuthenticateRequestProperties Properties { get; set; } = new();
}

internal sealed class XboxAuthenticateRequestProperties
{
    public string AuthMethod { get; set; } = "RPS";
    public string SiteName { get; set; } = "user.auth.xboxlive.com";
    public string RpsTicket { get; set; } = string.Empty;
}

internal sealed class XboxAuthorizeRequest
{
    public string RelyingParty { get; set; } = "http://xboxlive.com";
    public string TokenType { get; set; } = "JWT";

    [JsonPropertyName("Properties")]
    public XboxAuthorizeRequestProperties Properties { get; set; } = new();
}

internal sealed class XboxAuthorizeRequestProperties
{
    public string SandboxId { get; set; } = "RETAIL";
    public List<string> UserTokens { get; set; } = [];
}

internal sealed class XboxProfileRequest
{
    public List<string> settings { get; set; } = ["GameDisplayName"];
    public List<ulong> userIds { get; set; } = [];
}

internal sealed record XboxCatalogEntry
{
    public required string Pfn { get; init; }
    public required string Title { get; init; }
    public string? StoreProductId { get; init; }
    public string TitleId { get; init; } = string.Empty;
    public int? PlaytimeMinutes { get; init; }
    public DateTime? LastPlayed { get; init; }
}
