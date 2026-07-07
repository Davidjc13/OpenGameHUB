using System.Text.Json;

namespace OpenGameHUB.Services.Auth;

internal static class WebViewScriptHelper
{
    public static string? UnwrapJsonString(string? jsonResult)
    {
        if (string.IsNullOrWhiteSpace(jsonResult) || jsonResult == "null")
            return null;

        try
        {
            return JsonSerializer.Deserialize<string>(jsonResult);
        }
        catch
        {
            return jsonResult.Trim().Trim('"');
        }
    }
}
