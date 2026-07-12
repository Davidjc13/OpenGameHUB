using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace OpenGameHUB.Services.Covers;

internal static class CoverImageProcessor
{
    public const int MaxWidth = 600;
    public const int MaxHeight = 900;
    public const double TargetAspect = (double)MaxWidth / MaxHeight;

    private const long JpegQuality = 85L;

    public static bool TryNormalizeToCacheFile(string sourcePath, string destinationPath) =>
        TryProcess(sourcePath, destinationPath, cropToTargetAspect: true);

    public static bool TryResizeToCacheFile(string sourcePath, string destinationPath) =>
        TryNormalizeToCacheFile(sourcePath, destinationPath);

    public static bool TryReNormalizeInPlace(string cachePath)
    {
        if (!SafeImageValidator.IsValidImageFile(cachePath) || IsNormalized(cachePath))
            return true;

        var tempPath = cachePath + ".norm.tmp";
        try
        {
            if (!TryNormalizeToCacheFile(cachePath, tempPath))
                return false;

            File.Move(tempPath, cachePath, overwrite: true);
            return true;
        }
        catch
        {
            TryDelete(tempPath);
            return false;
        }
    }

    public static bool IsNormalized(string path)
    {
        if (!SafeImageValidator.IsValidImageFile(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            using var source = new Bitmap(stream);
            return source.Width == MaxWidth && source.Height == MaxHeight;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryProcess(string sourcePath, string destinationPath, bool cropToTargetAspect)
    {
        if (!SafeImageValidator.IsValidImageFile(sourcePath))
            return false;

        try
        {
            using var stream = File.OpenRead(sourcePath);
            using var source = new Bitmap(stream);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            using var normalized = cropToTargetAspect
                ? CropAndScale(source, MaxWidth, MaxHeight)
                : ScaleToFit(source, MaxWidth, MaxHeight);

            SaveAsJpeg(normalized, destinationPath);
            return SafeImageValidator.IsValidImageFile(destinationPath);
        }
        catch
        {
            TryDelete(destinationPath);
            return false;
        }
    }

    private static Bitmap CropAndScale(Bitmap source, int targetWidth, int targetHeight)
    {
        var targetAspect = (double)targetWidth / targetHeight;
        var sourceAspect = (double)source.Width / source.Height;

        Rectangle cropRect;
        if (sourceAspect > targetAspect)
        {
            var cropWidth = Math.Max(1, (int)Math.Round(source.Height * targetAspect));
            var cropX = Math.Max(0, (source.Width - cropWidth) / 2);
            cropRect = new Rectangle(cropX, 0, Math.Min(cropWidth, source.Width - cropX), source.Height);
        }
        else
        {
            var cropHeight = Math.Max(1, (int)Math.Round(source.Width / targetAspect));
            var cropY = Math.Max(0, (source.Height - cropHeight) / 2);
            cropRect = new Rectangle(0, cropY, source.Width, Math.Min(cropHeight, source.Height - cropY));
        }

        using var cropped = source.Clone(cropRect, source.PixelFormat);
        var output = new Bitmap(targetWidth, targetHeight);
        using var graphics = Graphics.FromImage(output);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(cropped, 0, 0, targetWidth, targetHeight);
        return output;
    }

    private static Bitmap ScaleToFit(Bitmap source, int maxWidth, int maxHeight)
    {
        var (width, height) = CalculateTargetSize(source.Width, source.Height, maxWidth, maxHeight);
        return width == source.Width && height == source.Height
            ? (Bitmap)source.Clone()
            : new Bitmap(source, new Size(width, height));
    }

    private static (int width, int height) CalculateTargetSize(
        int sourceWidth,
        int sourceHeight,
        int maxWidth,
        int maxHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return (maxWidth, maxHeight);

        var scale = Math.Min((double)maxWidth / sourceWidth, (double)maxHeight / sourceHeight);
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
