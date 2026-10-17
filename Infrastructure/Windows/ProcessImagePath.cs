using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace OpenGameHUB.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
internal static class ProcessImagePath
{
    private const uint QueryLimitedInformation = 0x1000;

    public static string? TryGet(Process process)
    {
        try
        {
            if (process.HasExited)
                return null;
        }
        catch (Exception)
        {
            return null;
        }

        try
        {
            return TryGet(process.Id);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string? TryGet(int processId)
    {
        var handle = OpenProcess(QueryLimitedInformation, false, (uint)processId);
        if (handle == IntPtr.Zero)
            return null;

        try
        {
            var capacity = 512;
            var builder = new StringBuilder(capacity);
            var size = capacity;
            if (!QueryFullProcessImageName(handle, 0, builder, ref size))
                return null;

            return builder.ToString();
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(
        IntPtr process,
        int flags,
        StringBuilder exeName,
        ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
