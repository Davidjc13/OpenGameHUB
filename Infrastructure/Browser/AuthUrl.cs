namespace OpenGameHUB.Infrastructure.Browser;

/// <summary>
/// Central, defensive URL parsing for the auth browser. Every URL that flows through the
/// embedded browser (navigations, responses, pasted redirects) must go through here so we
/// never inspect raw strings with <c>Contains</c>/<c>EndsWith</c>.
/// </summary>
internal static class AuthUrl
{
    /// <summary>
    /// Parses an absolute <b>HTTPS</b> URL. Non-absolute, non-HTTPS or malformed URLs are rejected.
    /// </summary>
    public static bool TryParse(string? url, out Uri uri)
    {
        uri = null!;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            return false;

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        uri = parsed;
        return true;
    }

    /// <summary>
    /// True when the parsed path equals the expected path or is nested under it, compared on
    /// whole segments (case-insensitive, ordinal). Avoids substring matches like a rogue
    /// <c>/notdev/apikey-evil</c> slipping through a naive <c>Contains</c>.
    /// </summary>
    public static bool PathMatches(Uri uri, string expectedPath)
    {
        var actual = uri.AbsolutePath.Trim('/');
        var expected = expectedPath.Trim('/');

        if (expected.Length == 0)
            return true;

        return actual.Equals(expected, StringComparison.OrdinalIgnoreCase)
            || actual.StartsWith(expected + "/", StringComparison.OrdinalIgnoreCase);
    }
}
