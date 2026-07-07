using System.Text.RegularExpressions;

namespace OpenGameHUB.Services.Auth;

internal sealed class SteamAuthCaptureStrategy : IAuthCaptureStrategy
{
    private static readonly string[] Hosts =
    [
        "steampowered.com",
        "steamcommunity.com"
    ];

    public const string LoginUrl =
        "https://steamcommunity.com/login/home/?goto=dev%2Fapikey";

    private static readonly Regex SteamIdRegex =
        new(@"g_steamID\s*=\s*[""'](\d{17})[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ApiKeyInputRegex =
        new(@"<input[^>]*\bvalue\s*=\s*[""']([A-F0-9]{32})[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ApiKeyTextRegex =
        new(@"\b([A-F0-9]{32})\b", RegexOptions.Compiled);

    private string? _steamId;
    private string? _apiKey;

    public string StartUrl => LoginUrl;

    public string WindowTitleKey => "EmbeddedBrowserSteamTitle";

    public string IntroKey => "EmbeddedBrowserSteamIntro";

    public IReadOnlyList<string> AllowedHosts => Hosts;

    public object? TryCaptureFromNavigation(string url) => null;

    public object? TryCaptureFromResponse(string requestUrl, string responseBody)
    {
        if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var uri))
            return null;

        if (!uri.Host.EndsWith("steamcommunity.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.EndsWith("steampowered.com", StringComparison.OrdinalIgnoreCase))
            return null;

        var steamId = TryParseSteamId(responseBody);
        if (!string.IsNullOrWhiteSpace(steamId))
            _steamId = steamId;

        if (requestUrl.Contains("/dev/apikey", StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = TryParseApiKey(responseBody);
            if (!string.IsNullOrWhiteSpace(apiKey))
                _apiKey = apiKey;
        }

        if (string.IsNullOrWhiteSpace(_steamId) && string.IsNullOrWhiteSpace(_apiKey))
            return null;

        return new SteamBrowserCaptureResult
        {
            SteamId = _steamId,
            ApiKey = _apiKey
        };
    }

    internal static string? TryParseSteamId(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var match = SteamIdRegex.Match(body);
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static string? TryParseApiKey(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var inputMatch = ApiKeyInputRegex.Match(body);
        if (inputMatch.Success)
            return inputMatch.Groups[1].Value;

        var textMatch = ApiKeyTextRegex.Match(body);
        return textMatch.Success ? textMatch.Groups[1].Value : null;
    }
}
