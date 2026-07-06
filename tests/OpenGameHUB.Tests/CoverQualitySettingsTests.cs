using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Services.Covers;

namespace OpenGameHUB.Tests;

public sealed class CoverQualitySettingsTests
{
    [Fact]
    public void Get_returns_profiles_for_each_mode()
    {
        Assert.True(CoverQualitySettings.Get(CoverQualityMode.High).ShowLibraryCovers);
        Assert.False(CoverQualitySettings.Get(CoverQualityMode.None).ShowLibraryCovers);
        Assert.Equal(96, CoverQualitySettings.Get(CoverQualityMode.Low).GridDecodeWidth);
    }
}
