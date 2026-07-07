using System.Globalization;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxCatalogMarket
{
    public static (string Market, string Language) Resolve()
    {
        string market;
        try
        {
            market = new RegionInfo(CultureInfo.CurrentUICulture.Name).TwoLetterISORegionName;
        }
        catch
        {
            market = "US";
        }

        if (string.IsNullOrWhiteSpace(market))
            market = "US";

        var language = CultureInfo.CurrentUICulture.Name.Replace('_', '-');
        if (string.IsNullOrWhiteSpace(language))
            language = "en-us";

        return (market.ToUpperInvariant(), language.ToLowerInvariant());
    }
}
