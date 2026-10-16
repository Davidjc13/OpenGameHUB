namespace OpenGameHUB.Services.Media;

public static class MediaVolumeTarget
{
    public static string? ProcessName(string? sourceAppId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppId))
            return null;

        var bang = sourceAppId.IndexOf('!');
        if (bang >= 0 && bang < sourceAppId.Length - 1)
        {
            var identity = SystemMediaSourceNames.Format(sourceAppId[(bang + 1)..]);
            if (!string.IsNullOrWhiteSpace(identity) && !IsDiscordVoice(identity))
                return identity;
        }

        var name = SystemMediaSourceNames.Format(sourceAppId);
        if (string.IsNullOrWhiteSpace(name) || IsDiscordVoice(name))
            return null;

        return name;
    }

    public static bool MatchesProcess(string target, string actual)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(actual))
            return false;

        if (actual.Equals(target, StringComparison.OrdinalIgnoreCase))
            return true;

        if (actual.Contains(target, StringComparison.OrdinalIgnoreCase))
            return true;

        return actual.Length >= 3
            && target.Contains(actual, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDiscordVoice(string? processName) =>
        processName?.StartsWith("Discord", StringComparison.OrdinalIgnoreCase) == true;
}
