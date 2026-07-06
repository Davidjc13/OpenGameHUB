namespace OpenGameHUB.Services.Updates;

internal static class ReleaseVersionComparer
{
    public static int Compare(string? latest, string? current)
    {
        if (TryParse(latest, out var latestParsed) && TryParse(current, out var currentParsed))
        {
            var channelCompare = ChannelRank(latestParsed.Channel).CompareTo(ChannelRank(currentParsed.Channel));
            if (channelCompare != 0)
                return channelCompare;

            var versionCompare = latestParsed.Version.CompareTo(currentParsed.Version);
            if (versionCompare != 0)
                return versionCompare;

            return CompareSuffix(latestParsed.Suffix, currentParsed.Suffix);
        }

        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase);
    }

    private static int ChannelRank(string channel) => channel switch
    {
        "stable" => 3,
        "beta" => 2,
        "alpha" => 1,
        _ => 0
    };

    private static int CompareSuffix(string? left, string? right)
    {
        var a = string.IsNullOrWhiteSpace(left) ? null : left.Trim();
        var b = string.IsNullOrWhiteSpace(right) ? null : right.Trim();

        if (a is null && b is null)
            return 0;
        if (a is null)
            return 1;
        if (b is null)
            return -1;

        if (int.TryParse(a, out var leftNum) && int.TryParse(b, out var rightNum))
            return leftNum.CompareTo(rightNum);

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParse(string? input, out (string Channel, Version Version, string? Suffix) result)
    {
        result = ("", new Version(0, 0), null);
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var tag = input.Trim();
        string channel;
        if (tag.StartsWith("alpha-", StringComparison.OrdinalIgnoreCase))
        {
            channel = "alpha";
            tag = tag[6..];
        }
        else if (tag.StartsWith("beta-", StringComparison.OrdinalIgnoreCase))
        {
            channel = "beta";
            tag = tag[5..];
        }
        else
        {
            channel = "stable";
        }

        var dashIndex = tag.IndexOf('-');
        var versionPart = dashIndex >= 0 ? tag[..dashIndex] : tag;
        var suffix = dashIndex >= 0 ? tag[(dashIndex + 1)..] : null;

        if (!Version.TryParse(versionPart, out var version))
            return false;

        result = (channel, version, suffix);
        return true;
    }
}
