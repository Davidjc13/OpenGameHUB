using Microsoft.Win32;

namespace OpenGameHUB.Services.Games;

internal sealed class SteamRunningAppReader : ISteamRunningReader
{
    public bool IsAppRunning(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
            return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\Apps\" + appId);
            return IsRunningValue(key?.GetValue("Running"));
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static bool IsRunningValue(object? value) =>
        value switch
        {
            int number => number != 0,
            long number => number != 0,
            string text => int.TryParse(text, out var parsed) && parsed != 0,
            _ => false
        };
}
