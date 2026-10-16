using System.Diagnostics;

namespace OpenGameHUB.Infrastructure;

public readonly record struct AppResourceSnapshot(
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    long ManagedHeapBytes,
    int ProcessThreadCount,
    int ThreadPoolThreadCount,
    int Gen0Collections);

public static class AppResourceMonitor
{
    public static AppResourceSnapshot Capture()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return new AppResourceSnapshot(
            process.WorkingSet64,
            process.PrivateMemorySize64,
            GC.GetTotalMemory(forceFullCollection: false),
            process.Threads.Count,
            ThreadPool.ThreadCount,
            GC.CollectionCount(0));
    }
}

public static class AppResourceFormatting
{
    public static string FormatMegabytes(long bytes) =>
        (bytes / (1024.0 * 1024.0)).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
}
