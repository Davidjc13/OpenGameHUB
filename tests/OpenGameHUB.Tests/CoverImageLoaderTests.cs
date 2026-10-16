using Avalonia.Media.Imaging;
using OpenGameHUB.Services.Covers;

namespace OpenGameHUB.Tests;

public sealed class CoverImageLoaderTests
{
    [Fact]
    public void TryDecode_returns_null_for_missing_file() =>
        Assert.Null(CoverImageLoader.TryDecode(
            Path.Combine(Path.GetTempPath(), "ogh-missing-cover.jpg"),
            96,
            BitmapInterpolationMode.LowQuality));

    [Fact]
    public void TryDecode_returns_null_when_width_is_not_positive() =>
        Assert.Null(CoverImageLoader.TryDecode("any.jpg", 0, BitmapInterpolationMode.LowQuality));

    [Fact]
    public void MaxFallbackDecodeWidth_caps_full_resolution_fallback() =>
        Assert.Equal(512, CoverImageLoader.MaxFallbackDecodeWidth);
}
