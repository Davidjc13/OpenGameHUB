using System.Text.Json;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxGamePassProductFilter
{
    public static bool IsInstallableOnPc(JsonElement product)
    {
        var title = ReadProductTitle(product);
        if (title.Contains("(PC)", StringComparison.OrdinalIgnoreCase)
            || title.Contains("(Windows)", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (product.TryGetProperty("Properties", out var productProperties))
        {
            if (productProperties.TryGetProperty("ProductGroupName", out var groupName)
                && (groupName.GetString() ?? string.Empty).Contains("PC", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (productProperties.TryGetProperty("Attributes", out var attributes)
                && HasPcInstallAttribute(attributes))
            {
                return true;
            }
        }

        if (!product.TryGetProperty("DisplaySkuAvailabilities", out var skuAvailabilities))
            return false;

        var sawPcOrXpaTerms = false;

        foreach (var skuAvailability in skuAvailabilities.EnumerateArray())
        {
            if (!skuAvailability.TryGetProperty("Sku", out var sku)
                || !sku.TryGetProperty("Properties", out var properties)
                || !properties.TryGetProperty("InstallationTerms", out var installationTerms))
            {
                continue;
            }

            var terms = installationTerms.GetString() ?? string.Empty;
            if (HasPcOrXpaInstallationTerms(terms))
                sawPcOrXpaTerms = true;
        }

        if (sawPcOrXpaTerms)
            return true;

        return false;
    }

    private static bool HasPcInstallAttribute(JsonElement attributes)
    {
        foreach (var attribute in attributes.EnumerateArray())
        {
            if (!attribute.TryGetProperty("Name", out var nameElement))
                continue;

            var name = nameElement.GetString();
            if (string.Equals(name, "PcGamePad", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "XPA", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPcOrXpaInstallationTerms(string terms) =>
        terms.Contains("RestrictivePC", StringComparison.OrdinalIgnoreCase)
        || terms.Contains("InstallationTermsPC", StringComparison.OrdinalIgnoreCase)
        || terms.Contains("RestrictiveXPA", StringComparison.OrdinalIgnoreCase)
        || terms.Contains("InstallationTermsXPA", StringComparison.OrdinalIgnoreCase);

    private static string ReadProductTitle(JsonElement product)
    {
        if (!product.TryGetProperty("LocalizedProperties", out var localized)
            || localized.ValueKind != JsonValueKind.Array
            || localized.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        return localized[0].TryGetProperty("ProductTitle", out var title)
            ? title.GetString() ?? string.Empty
            : string.Empty;
    }
}
