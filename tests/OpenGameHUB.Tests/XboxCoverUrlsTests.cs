using System.Text.Json;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Tests;

public sealed class XboxCoverUrlsTests
{
    [Fact]
    public void SelectPosterUrl_prefers_portrait_poster()
    {
        const string json = """
            {
              "LocalizedProperties": [{
                "Images": [
                  { "ImagePurpose": "BoxArt", "Width": 1080, "Height": 1080, "Uri": "//store-images.s-microsoft.com/image/box" },
                  { "ImagePurpose": "Screenshot", "Width": 1920, "Height": 1080, "Uri": "//store-images.s-microsoft.com/image/shot" },
                  { "ImagePurpose": "Poster", "Width": 720, "Height": 1080, "Uri": "//store-images.s-microsoft.com/image/poster" }
                ]
              }]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var url = XboxCoverUrls.SelectPosterUrl(document.RootElement);

        Assert.Equal("https://store-images.s-microsoft.com/image/poster", url);
    }

    [Fact]
    public void SelectPosterUrl_uses_taller_image_when_poster_is_missing()
    {
        const string json = """
            {
              "LocalizedProperties": [{
                "Images": [
                  { "ImagePurpose": "Screenshot", "Width": 1920, "Height": 1080, "Uri": "//store-images.s-microsoft.com/image/shot" },
                  { "ImagePurpose": "TitledHeroArt", "Width": 600, "Height": 900, "Uri": "http://store-images.s-microsoft.com/image/hero" }
                ]
              }]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var url = XboxCoverUrls.SelectPosterUrl(document.RootElement);

        Assert.Equal("https://store-images.s-microsoft.com/image/hero", url);
    }
}
