using System.Drawing;
using System.Drawing.Imaging;
using OpenGameHUB.Services.Covers;

namespace OpenGameHUB.Tests;

public sealed class CoverImageProcessorTests : IDisposable
{
    private readonly string _tempDir;

    public CoverImageProcessorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "OpenGameHUB.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // optional cleanup
        }
    }

    [Fact]
    public void TryNormalizeToCacheFile_outputs_600x900_for_landscape_source()
    {
        var sourcePath = Path.Combine(_tempDir, "landscape.png");
        var outputPath = Path.Combine(_tempDir, "cover.jpg");
        SaveBitmap(sourcePath, 920, 430, Color.SteelBlue);

        Assert.True(CoverImageProcessor.TryNormalizeToCacheFile(sourcePath, outputPath));
        Assert.True(CoverImageProcessor.IsNormalized(outputPath));

        using var output = new Bitmap(outputPath);
        Assert.Equal(600, output.Width);
        Assert.Equal(900, output.Height);
    }

    [Fact]
    public void TryNormalizeToCacheFile_outputs_600x900_for_square_source()
    {
        var sourcePath = Path.Combine(_tempDir, "square.png");
        var outputPath = Path.Combine(_tempDir, "cover-square.jpg");
        SaveBitmap(sourcePath, 512, 512, Color.IndianRed);

        Assert.True(CoverImageProcessor.TryNormalizeToCacheFile(sourcePath, outputPath));
        Assert.True(CoverImageProcessor.IsNormalized(outputPath));
    }

    [Fact]
    public void TryReNormalizeInPlace_skips_already_normalized_file()
    {
        var sourcePath = Path.Combine(_tempDir, "source.png");
        var cachePath = Path.Combine(_tempDir, "cache.jpg");
        SaveBitmap(sourcePath, 300, 450, Color.MediumSeaGreen);

        Assert.True(CoverImageProcessor.TryNormalizeToCacheFile(sourcePath, cachePath));
        var lastWrite = File.GetLastWriteTimeUtc(cachePath);

        Assert.True(CoverImageProcessor.TryReNormalizeInPlace(cachePath));
        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(cachePath));
    }

    private static void SaveBitmap(string path, int width, int height, Color color)
    {
        using var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        bitmap.Save(path, ImageFormat.Png);
    }
}
