using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenGameHUB.Services.Auth;

internal sealed class EpicAuthCaptureStrategy : IAuthCaptureStrategy
{
    public const string LoginUrl = "https://legendary.gl/epiclogin";

    private static readonly string[] Hosts =
    [
        "legendary.gl",
        "epicgames.com",
        "unrealengine.com"
    ];

    private static readonly Regex AuthorizationCodeRegex =
        new(@"""authorizationCode""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string StartUrl => LoginUrl;

    public string WindowTitleKey => "EmbeddedBrowserEpicTitle";

    public string IntroKey => "EmbeddedBrowserEpicIntro";

    public IReadOnlyList<string> AllowedHosts => Hosts;

    public object? TryCaptureFromNavigation(string url) => null;

    public async Task<object?> TryCaptureFromDomAsync(
        Func<string, Task<string?>> executeScriptAsync,
        string currentUrl)
    {
        if (!currentUrl.Contains("/id/api/redirect", StringComparison.OrdinalIgnoreCase)
            && !currentUrl.Contains("legendary.gl", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var raw = await executeScriptAsync("document.body ? document.body.innerText : ''");
        var body = WebViewScriptHelper.UnwrapJsonString(raw);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        return TryParseAuthorizationCode(body);
    }

    internal static string? TryParseAuthorizationCode(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var trimmed = body.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.TryGetProperty("authorizationCode", out var codeElement))
                {
                    var code = codeElement.GetString();
                    if (!string.IsNullOrWhiteSpace(code))
                        return code;
                }
            }
            catch
            {
                // fall through to regex
            }
        }

        var match = AuthorizationCodeRegex.Match(trimmed);
        return match.Success ? match.Groups[1].Value : null;
    }
}
