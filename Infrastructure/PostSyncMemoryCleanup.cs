using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenGameHUB.Infrastructure;

/// <summary>
/// Returns the process toward a cold-start idle footprint after library sync (GC compact + working-set trim).
/// Disable with OPENGAMEHUB_NO_POST_SYNC_IDLE=1. Opt out of trim only with OPENGAMEHUB_NO_POST_SYNC_TRIM=1.
/// Force trim on other paths with OPENGAMEHUB_TRIM_WORKING_SET=1.
/// </summary>
internal static class PostSyncMemoryCleanup
{
    public static void ApplyIdleBaselineAfterSync()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable("OPENGAMEHUB_NO_POST_SYNC_IDLE"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: true, compacting: true);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[{0}] operation=ApplyIdleBaselineAfterSync.gc message={1}",
                nameof(PostSyncMemoryCleanup),
                ex.Message);
        }

        TryTrimWorkingSet(forceAfterSync: true);
    }

    public static void TryTrimWorkingSet(bool forceAfterSync = false)
    {
        if (!ShouldTrimWorkingSet(forceAfterSync))
            return;

        try
        {
            var handle = Process.GetCurrentProcess().Handle;
            _ = SetProcessWorkingSetSize(handle, unchecked((nuint)(-1)), unchecked((nuint)(-1)));
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[{0}] operation=TryTrimWorkingSet message={1}",
                nameof(PostSyncMemoryCleanup),
                ex.Message);
        }
    }

    private static bool ShouldTrimWorkingSet(bool forceAfterSync)
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable("OPENGAMEHUB_NO_POST_SYNC_TRIM"),
                "1",
                StringComparison.Ordinal))
        {
            return false;
        }

        if (forceAfterSync)
            return true;

        return string.Equals(
            Environment.GetEnvironmentVariable("OPENGAMEHUB_TRIM_WORKING_SET"),
            "1",
            StringComparison.Ordinal);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, nuint dwMinimumWorkingSetSize, nuint dwMaximumWorkingSetSize);
}
