using Avalonia.Media.Imaging;

namespace OpenGameHUB.Services;

internal static class CoverImageLoader
{
    /// <summary>Matches grid tile width in MainWindow.axaml (180px).</summary>
    public const int ThumbnailWidth = 180;

    /// <summary>Slightly larger decode for the detail panel (200px wide).</summary>
    public const int DetailWidth = 200;

    private static readonly SemaphoreSlim LoadSemaphore = new(3, 3);

    public static async Task<T> RunThrottledAsync<T>(Func<Task<T>> action)
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

    public static Task<Bitmap?> LoadDecodedAsync(string path, int decodeWidth) =>
        RunThrottledAsync(() => Task.Run(() => TryDecode(path, decodeWidth)));

    private static Bitmap? TryDecode(string path, int decodeWidth)
    {
        if (!SafeImageValidator.IsValidImageFile(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.MediumQuality);
        }
        catch
        {
            try
            {
                return new Bitmap(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
