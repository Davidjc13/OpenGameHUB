namespace OpenGameHUB.Providers.Xbox;

internal static class XboxTitleFilter
{
    public static bool IsPcLibraryTitle(XboxTitleEntry title) =>
        string.Equals(title.type, "Game", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(title.pfn)
        && (HasPcDevice(title) || HasPcStoreListing(title));

    private static bool HasPcDevice(XboxTitleEntry title) =>
        title.devices.Any(device => device.Equals("PC", StringComparison.OrdinalIgnoreCase));

    private static bool HasPcStoreListing(XboxTitleEntry title) =>
        !string.IsNullOrWhiteSpace(title.modernTitleId)
        || !string.IsNullOrWhiteSpace(title.windowsPhoneProductId);
}
