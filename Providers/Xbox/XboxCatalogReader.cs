using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxCatalogReader
{
    public static bool IsCloudLibraryAvailable() => XboxAccountClient.IsAuthenticated();

    public static bool MatchesInstalledGame(UnifiedGame installed, XboxCatalogEntry entry)
    {
        if (installed.Platform != Platform.GamePass)
            return false;

        if (!string.IsNullOrWhiteSpace(installed.PlatformGameId)
            && string.Equals(installed.PlatformGameId, entry.Pfn, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return MetadataSearchHelper.NormalizeTitle(installed.Title)
            .Equals(MetadataSearchHelper.NormalizeTitle(entry.Title), StringComparison.OrdinalIgnoreCase);
    }

    public static LaunchSpec BuildInstallLaunchSpec(XboxCatalogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.StoreProductId))
            return LaunchSpec.Protocol($"msxbox://game/?productId={entry.StoreProductId}");

        return LaunchSpec.Protocol($"ms-windows-store://pdp/?PFN={Uri.EscapeDataString(entry.Pfn)}");
    }
}
