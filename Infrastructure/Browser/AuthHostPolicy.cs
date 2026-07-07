namespace OpenGameHUB.Infrastructure.Browser;

internal static class AuthHostPolicy
{
    public static bool IsHostAllowed(string? host, IReadOnlyList<string> allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(host) || allowedHosts.Count == 0)
            return false;

        var hostLabels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (hostLabels.Length == 0)
            return false;

        foreach (var allowed in allowedHosts)
        {
            if (IsSameOrSubdomain(hostLabels, allowed))
                return true;
        }

        return false;
    }

    // Matches by DNS label from the right, so "steamcommunity.com" allows "steamcommunity.com" and
    // "store.steamcommunity.com" but never "evilsteamcommunity.com" or "steamcommunity.com.evil.tld".
    private static bool IsSameOrSubdomain(string[] hostLabels, string allowed)
    {
        var allowedLabels = allowed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (allowedLabels.Length == 0 || hostLabels.Length < allowedLabels.Length)
            return false;

        for (var i = 1; i <= allowedLabels.Length; i++)
        {
            var hostLabel = hostLabels[^i];
            var allowedLabel = allowedLabels[^i];
            if (!hostLabel.Equals(allowedLabel, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
