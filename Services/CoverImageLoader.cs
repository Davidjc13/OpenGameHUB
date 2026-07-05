namespace OpenGameHUB.Services;

internal static class CoverImageLoader
{
    public const int ThumbnailWidth = 180;

    private static readonly SemaphoreSlim LoadSemaphore = new(4, 4);

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
}
