using System.Text.Json;

namespace OpenGameHUB.Services.Auth;

internal sealed class SteamAuthCaptureStrategy : IAuthCaptureStrategy
{
    public const string LoginUrl =
        "https://steamcommunity.com/login/home/?goto=dev%2Fapikey";

    public const string ApiKeyUrl = "https://steamcommunity.com/dev/apikey";

    private const string CaptureScript = """
        (() => {
          let steamId = '';
          if (typeof g_steamID !== 'undefined' && g_steamID)
            steamId = String(g_steamID);

          let apiKey = '';
          const inputs = document.querySelectorAll('input');
          for (const input of inputs) {
            const value = (input.value || '').trim();
            if (/^[A-F0-9]{32}$/i.test(value)) {
              apiKey = value;
              break;
            }
          }

          if (!apiKey) {
            const match = (document.body?.innerText || '').match(/\b([A-F0-9]{32})\b/i);
            if (match)
              apiKey = match[1];
          }

          return JSON.stringify({ steamId, apiKey });
        })();
        """;

    public string StartUrl => LoginUrl;

    public string WindowTitleKey => "EmbeddedBrowserSteamTitle";

    public string IntroKey => "EmbeddedBrowserSteamIntro";

    public string? WaitingStatusKey => "EmbeddedBrowserSteamWaitingForKey";

    public object? TryCaptureFromNavigation(string url) => null;

    public async Task<object?> TryCaptureFromDomAsync(
        Func<string, Task<string?>> executeScriptAsync,
        string currentUrl)
    {
        if (!currentUrl.Contains("steamcommunity.com", StringComparison.OrdinalIgnoreCase))
            return null;

        var raw = await executeScriptAsync(CaptureScript);
        var json = WebViewScriptHelper.UnwrapJsonString(raw);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var steamId = document.RootElement.TryGetProperty("steamId", out var steamIdElement)
                ? steamIdElement.GetString()
                : null;
            var apiKey = document.RootElement.TryGetProperty("apiKey", out var apiKeyElement)
                ? apiKeyElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(steamId) && string.IsNullOrWhiteSpace(apiKey))
                return null;

            return new SteamBrowserCaptureResult
            {
                SteamId = string.IsNullOrWhiteSpace(steamId) ? null : steamId.Trim(),
                ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim()
            };
        }
        catch
        {
            return null;
        }
    }
}
