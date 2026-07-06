using System.Text.Json;

namespace OpenGameHUB.Services.Epic;

internal static class EpicKeyImageHelper
{
    private static readonly string[] PreferredTypes =
    [
        "DieselGameBoxTall",
        "DieselStoreFrontTall",
        "DieselGameBox",
        "DieselStoreFrontWide",
        "OfferImageTall",
        "Thumbnail"
    ];

    public static string? PickBestCoverUrl(JsonElement keyImages)
    {
        if (keyImages.ValueKind != JsonValueKind.Array)
            return null;

        var images = new List<(string Type, string Url)>();
        foreach (var item in keyImages.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
                continue;

            if (!item.TryGetProperty("url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String)
                continue;

            var url = urlElement.GetString();
            if (string.IsNullOrWhiteSpace(url))
                continue;

            images.Add((typeElement.GetString()!, url));
        }

        if (images.Count == 0)
            return null;

        foreach (var preferred in PreferredTypes)
        {
            var match = images.FirstOrDefault(image =>
                string.Equals(image.Type, preferred, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Url))
                return match.Url;
        }

        return images[0].Url;
    }
}
