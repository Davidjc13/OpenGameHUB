using System.Drawing;
using System.Drawing.Imaging;

namespace OpenGameHUB.Services;

internal static class CoverImageProcessor
{
    public const int MaxWidth = 600;
    public const int MaxHeight = 900;
    private const long JpegQuality = 85L;

    public static bool TryResizeToCacheFile(string sourcePath, string destinationPath)
    {
        if (!SafeImageValidator.IsValidImageFile(sourcePath))
            return false;

        try
        {
            using var stream = File.OpenRead(sourcePath);
            using var source = new Bitmap(stream);
            var (width, height) = CalculateTargetSize(source.Width, source.Height);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            using var resized = width == source.Width && height == source.Height
                ? (Bitmap)source.Clone()
                : new Bitmap(source, new Size(width, height));

            SaveAsJpeg(resized, destinationPath);
            return SafeImageValidator.IsValidImageFile(destinationPath);
        }
        catch
        {
            TryDelete(destinationPath);
            return TryCopyValidatedImage(sourcePath, destinationPath);
        }
    }

    private static (int width, int height) CalculateTargetSize(int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return (MaxWidth, MaxHeight);

        var scale = Math.Min(
            (double)MaxWidth / sourceWidth,
            (double)MaxHeight / sourceHeight);

        if (scale >= 1.0)
            return (sourceWidth, sourceHeight);

        return (
            Math.Max(1, (int)Math.Round(sourceWidth * scale)),
            Math.Max(1, (int)Math.Round(sourceHeight * scale)));
    }

    private static void SaveAsJpeg(Bitmap bitmap, string destinationPath)
    {
        var codec = ImageCodecInfo.GetImageEncoders()
            .First(encoder => encoder.FormatID == ImageFormat.Jpeg.Guid);

        using var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);
        bitmap.Save(destinationPath, codec, encoderParams);
    }

    private static bool TryCopyValidatedImage(string sourcePath, string destinationPath)
    {
        if (!SafeImageValidator.IsValidImageFile(sourcePath))
            return false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            return SafeImageValidator.IsValidImageFile(destinationPath);
        }
        catch
        {
            TryDelete(destinationPath);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // optional
        }
    }
}
