using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenGameHUB.Services.Media;

namespace OpenGameHUB.Infrastructure.Media;

internal static class SpotifyWindowMedia
{
    private const int WindowMessageAppCommand = 0x0319;
    private const int AppCommandNext = 11;
    private const int AppCommandPrevious = 12;
    private const int AppCommandPlayPause = 14;

    public static bool TryGetTitle(out string title)
    {
        title = string.Empty;
        try
        {
            foreach (var process in Process.GetProcessesByName("Spotify"))
            {
                using (process)
                {
                    if (process.MainWindowHandle == IntPtr.Zero)
                        continue;

                    var windowTitle = process.MainWindowTitle;
                    if (string.IsNullOrWhiteSpace(windowTitle))
                        continue;

                    title = windowTitle;
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }

        return false;
    }

    public static bool TrySend(MediaTransport transport)
    {
        if (!TryGetHandle(out var handle))
            return false;

        var command = transport switch
        {
            MediaTransport.Next => AppCommandNext,
            MediaTransport.Previous => AppCommandPrevious,
            _ => AppCommandPlayPause
        };
        SendMessage(handle, WindowMessageAppCommand, handle, (IntPtr)(command << 16));
        return true;
    }

    private static bool TryGetHandle(out IntPtr handle)
    {
        handle = IntPtr.Zero;
        try
        {
            foreach (var process in Process.GetProcessesByName("Spotify"))
            {
                using (process)
                {
                    if (process.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainWindowTitle))
                        continue;

                    handle = process.MainWindowHandle;
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }

        return false;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
