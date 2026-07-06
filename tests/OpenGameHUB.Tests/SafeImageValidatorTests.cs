using OpenGameHUB.Services.Covers;

namespace OpenGameHUB.Tests;

public sealed class SafeImageValidatorTests
{
    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png; charset=utf-8", true)]
    [InlineData("text/html", false)]
    [InlineData(null, false)]
    public void IsAllowedMimeType_validates_known_types(string? contentType, bool expected)
    {
        Assert.Equal(expected, SafeImageValidator.IsAllowedMimeType(contentType));
    }

    [Fact]
    public void HasValidImageSignature_detects_jpeg_png_gif_webp()
    {
        Assert.True(SafeImageValidator.HasValidImageSignature([0xFF, 0xD8, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
        Assert.True(SafeImageValidator.HasValidImageSignature([0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
        Assert.True(SafeImageValidator.HasValidImageSignature([(byte)'G', (byte)'I', (byte)'F', 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
        Assert.True(SafeImageValidator.HasValidImageSignature([
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0,
            (byte)'W', (byte)'E', (byte)'B', (byte)'P'
        ]));
        Assert.False(SafeImageValidator.HasValidImageSignature([0x00, 0x01, 0x02]));
    }

    [Fact]
    public void IsValidImageFile_reads_temp_png_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ogh-test-{Guid.NewGuid():N}.png");
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 };
        File.WriteAllBytes(path, bytes);

        try
        {
            Assert.True(SafeImageValidator.IsValidImageFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
