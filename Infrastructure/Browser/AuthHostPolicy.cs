namespace OpenGameHUB.Infrastructure.Browser;

internal static class AuthHostPolicy
{
    public static bool IsHostAllowed(string host, IReadOnlyList<string> allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(host) || allowedHosts.Count == 0)
            return false;

        foreach (var allowed in allowedHosts)
        {
            if (host.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;

            if (host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
