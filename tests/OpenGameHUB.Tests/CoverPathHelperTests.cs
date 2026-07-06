using OpenGameHUB.Services.Covers;

namespace OpenGameHUB.Tests;

public sealed class CoverPathHelperTests
{
    [Fact]
    public void SanitizeFileName_replaces_invalid_characters()
    {
        var sanitized = CoverPathHelper.SanitizeFileName("steam:store/730*test");
        Assert.DoesNotContain('*', sanitized);
        Assert.DoesNotContain('/', sanitized);
    }

    [Fact]
    public void ResolveExistingPath_returns_valid_cover_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ogh-cover-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);

        try
        {
            var resolved = CoverPathHelper.ResolveExistingPath("steam:store:730", path);
            Assert.Equal(path, resolved);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetCachePath_uses_sanitized_game_id()
    {
        var path = CoverPathHelper.GetCachePath("steam:store/730");
        Assert.EndsWith("steam_store_730.jpg", path);
    }
}
