using System.Text.Json;
using OpenGameHUB.Providers.Epic;

namespace OpenGameHUB.Tests;

public sealed class EpicKeyImageHelperTests
{
    [Fact]
    public void PickBestCoverUrl_prefers_tall_box_art()
    {
        using var document = JsonDocument.Parse("""
            [
              {"type":"Thumbnail","url":"https://cdn/thumb.jpg"},
              {"type":"DieselGameBoxTall","url":"https://cdn/tall.jpg"}
            ]
            """);

        Assert.Equal("https://cdn/tall.jpg", EpicKeyImageHelper.PickBestCoverUrl(document.RootElement));
    }

    [Fact]
    public void PickBestCoverUrl_returns_null_for_invalid_payload()
    {
        using var document = JsonDocument.Parse("""{"type":"Thumbnail"}""");
        Assert.Null(EpicKeyImageHelper.PickBestCoverUrl(document.RootElement));
    }
}
