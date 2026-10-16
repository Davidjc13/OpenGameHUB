using OpenGameHUB.Infrastructure.Browser;

namespace OpenGameHUB.Infrastructure.Http;

internal static class CoverImageHostPolicy
{
    internal static readonly string[] AllowedHosts =
    [
        "steamstatic.com",
        "igdb.com",
        "steamgriddb.com",
        "wikimedia.org",
        "epicgames.com",
        "unrealengine.com",
        "ubistatic3-a.akamaihd.net",
        "media-rockstargames-com.akamaized.net",
        "bnetproduct-a.akamaihd.net",
        "store-images.s-microsoft.com",
    ];

    internal static bool IsHostAllowed(string? host) =>
        AuthHostPolicy.IsHostAllowed(host, AllowedHosts);
}
