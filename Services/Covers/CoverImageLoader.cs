using Avalonia.Media.Imaging;

namespace OpenGameHUB.Services.Covers;

internal static class CoverImageLoader
{
    /// <summary>High-quality grid tile width (matches MainWindow.axaml 180px).</summary>
    public const int HighGridWidth = 180;

    /// <summary>High-quality detail panel width.</summary>
    public const int HighDetailWidth = 280;

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

    private static Bitmap? TryDecode(
        string path,
        int decodeWidth,
        BitmapInterpolationMode interpolation)
    {
        if (!SafeImageValidator.IsValidImageFile(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, decodeWidth, interpolation);
        }
        catch
        {
            if (decodeWidth <= 0)
                return null;

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
