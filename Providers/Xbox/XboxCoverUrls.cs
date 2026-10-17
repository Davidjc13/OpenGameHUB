using System.Text.Json;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxCoverUrls
{
    public static string? SelectPosterUrl(JsonElement product)
    {
        if (!product.TryGetProperty("LocalizedProperties", out var localized)
            || localized.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var images = new List<StoreImage>();
        foreach (var properties in localized.EnumerateArray())
        {
            if (!properties.TryGetProperty("Images", out var imageElements)
                || imageElements.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var image in imageElements.EnumerateArray())
            {
                var purpose = image.TryGetProperty("ImagePurpose", out var purposeElement)
                    ? purposeElement.GetString()
                    : null;
                var uri = image.TryGetProperty("Uri", out var uriElement)
                    ? uriElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(purpose) || string.IsNullOrWhiteSpace(uri))
                    continue;

                images.Add(new StoreImage(
                    purpose,
                    ReadInt(image, "Width"),
                    ReadInt(image, "Height"),
                    uri));
            }
        }

        var poster = images
            .Where(image => image.Purpose.Equals("Poster", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(image => image.Height)
            .FirstOrDefault();
        if (poster is not null)
            return NormalizeUri(poster.Uri);

        var portrait = images
            .Where(image => image.Height > image.Width && image.Height > 0)
            .OrderByDescending(image => image.Height)
            .FirstOrDefault();
        if (portrait is not null)
            return NormalizeUri(portrait.Uri);

        var boxArt = images.FirstOrDefault(image =>
            image.Purpose.Equals("BoxArt", StringComparison.OrdinalIgnoreCase));
        return boxArt is null ? null : NormalizeUri(boxArt.Uri);
    }

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number)
            ? number
            : 0;

    private static string NormalizeUri(string uri)
    {
        if (uri.StartsWith("//", StringComparison.Ordinal))
            return "https:" + uri;

        if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return "https://" + uri["http://".Length..];

        return uri;
    }

    private sealed record StoreImage(string Purpose, int Width, int Height, string Uri);
}
