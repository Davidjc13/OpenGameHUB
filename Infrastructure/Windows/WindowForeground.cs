using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace OpenGameHUB.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowForeground
{
    private const int SwShow = 5;

    public static void TryActivate(Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return;

        ShowWindow(handle, SwShow);
        SetForegroundWindow(handle);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
}
