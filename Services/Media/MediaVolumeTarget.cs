namespace OpenGameHUB.Services.Media;

public static class MediaVolumeTarget
{
    public static string? ProcessName(string? sourceAppId)
    {
        var name = SystemMediaSourceNames.Format(sourceAppId);
        if (string.IsNullOrWhiteSpace(name) || IsDiscordVoice(name))
            return null;

        return name;
    }

    public static bool IsDiscordVoice(string? processName) =>
        processName?.StartsWith("Discord", StringComparison.OrdinalIgnoreCase) == true;
}
