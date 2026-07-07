using System.Text.Json;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxGamePassProductFilter
{
    public static bool IsInstallableOnPc(JsonElement product)
    {
        if (!product.TryGetProperty("DisplaySkuAvailabilities", out var skuAvailabilities))
            return false;

        var sawConsoleOnly = false;

        foreach (var skuAvailability in skuAvailabilities.EnumerateArray())
        {
            if (!skuAvailability.TryGetProperty("Sku", out var sku)
                || !sku.TryGetProperty("Properties", out var properties))
            {
                continue;
            }

            if (!properties.TryGetProperty("InstallationTerms", out var installationTerms))
                continue;

            var terms = installationTerms.GetString() ?? string.Empty;
            if (terms.Contains("RestrictivePC", StringComparison.OrdinalIgnoreCase)
                || terms.Contains("InstallationTermsPC", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (terms.Contains("RestrictiveXbox", StringComparison.OrdinalIgnoreCase)
                || terms.Contains("RestrictiveConsole", StringComparison.OrdinalIgnoreCase))
            {
                sawConsoleOnly = true;
            }
        }

        if (sawConsoleOnly)
            return false;

        if (product.TryGetProperty("Properties", out var productProperties)
            && productProperties.TryGetProperty("Attributes", out var attributes))
        {
            foreach (var attribute in attributes.EnumerateArray())
            {
                if (!attribute.TryGetProperty("Name", out var nameElement))
                    continue;

                if (string.Equals(nameElement.GetString(), "PcGamePad", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        var title = ReadProductTitle(product);
        if (title.Contains("(PC)", StringComparison.OrdinalIgnoreCase))
            return true;

        if (product.TryGetProperty("Properties", out var props)
            && props.TryGetProperty("ProductGroupName", out var groupName)
            && (groupName.GetString() ?? string.Empty).Contains("PC", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

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
