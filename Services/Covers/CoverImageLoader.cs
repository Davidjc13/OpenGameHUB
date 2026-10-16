using System.Diagnostics;
using Avalonia.Media.Imaging;

namespace OpenGameHUB.Services.Covers;

internal static class CoverImageLoader
{
    /// <summary>High-quality grid tile width (matches MainWindow.axaml 180px).</summary>
    public const int HighGridWidth = 180;

    /// <summary>High-quality detail panel width.</summary>
    public const int HighDetailWidth = 280;

    /// <summary>Upper bound if the requested decode width fails. Avoids full-resolution bitmaps.</summary>
    public const int MaxFallbackDecodeWidth = 512;

    private static readonly SemaphoreSlim LoadSemaphore = new(2, 2);

    public static Task<Bitmap?> LoadDecodedAsync(
        string path,
        int decodeWidth,
        BitmapInterpolationMode interpolation = BitmapInterpolationMode.LowQuality) =>
        RunThrottledAsync(() => Task.Run(() => TryDecode(path, decodeWidth, interpolation)));

    private static async Task<T> RunThrottledAsync<T>(Func<Task<T>> action)
    {
        await LoadSemaphore.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            LoadSemaphore.Release();
        }
    }

    internal static Bitmap? TryDecode(
        string path,
        int decodeWidth,
        BitmapInterpolationMode interpolation)
    {
        if (!SafeImageValidator.IsValidImageFile(path) || decodeWidth <= 0)
            return null;

        if (TryDecodeToWidth(path, decodeWidth, interpolation, out var bitmap))
            return bitmap;

        if (decodeWidth != MaxFallbackDecodeWidth
            && TryDecodeToWidth(path, MaxFallbackDecodeWidth, interpolation, out bitmap))
            return bitmap;

        Debug.WriteLine($"Cover decode failed for {path}");
        return null;
    }

    private static bool TryDecodeToWidth(
        string path,
        int decodeWidth,
        BitmapInterpolationMode interpolation,
        out Bitmap? bitmap)
    {
        bitmap = null;
        try
        {
            using var stream = File.OpenRead(path);
            bitmap = Bitmap.DecodeToWidth(stream, decodeWidth, interpolation);
            return bitmap is not null;
        }
        catch
        {
            bitmap?.Dispose();
            bitmap = null;
            return false;
        }
    }
}
