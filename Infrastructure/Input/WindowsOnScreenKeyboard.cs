using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenGameHUB.Infrastructure.Input;

public static class WindowsOnScreenKeyboard
{
    public static async Task ShowAsync(IntPtr hwnd, CancellationToken cancellationToken = default)
    {
        EnsureTabTip();
        try
        {
            await Task.Delay(200, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (TryToggle(hwnd))
            return;

        TryStart("osk.exe");
    }

    public static bool TryShow() => TryShow(IntPtr.Zero);

    public static bool TryShow(IntPtr hwnd)
    {
        EnsureTabTip();
        return TryToggle(hwnd) || TryStart("osk.exe");
    }

    private static void EnsureTabTip()
    {
        foreach (var path in TabTipPaths())
        {
            if (TryStart(path))
                return;
        }
    }

    private static bool TryToggle(IntPtr hwnd)
    {
        try
        {
            var type = Type.GetTypeFromCLSID(UiHostNoLaunchClsid);
            if (type is null)
                return false;

            var host = Activator.CreateInstance(type);
            if (host is null)
                return false;

            try
            {
                var window = hwnd != IntPtr.Zero ? hwnd : GetDesktopWindow();
                ((ITipInvocation)host).Toggle(window);
                return true;
            }
            finally
            {
                Marshal.ReleaseComObject(host);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static IEnumerable<string> TabTipPaths()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            @"Microsoft Shared\ink\TabTip.exe");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            @"Common Files\Microsoft Shared\ink\TabTip.exe");

        var programW6432 = Environment.GetEnvironmentVariable("ProgramW6432");
        if (!string.IsNullOrWhiteSpace(programW6432))
        {
            yield return Path.Combine(programW6432, @"Common Files\Microsoft Shared\ink\TabTip.exe");
        }
    }

    private static bool TryStart(string fileName)
    {
        try
        {
            if (!string.Equals(fileName, "osk.exe", StringComparison.OrdinalIgnoreCase)
                && !File.Exists(fileName))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo(fileName)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static readonly Guid UiHostNoLaunchClsid = new("4ce576fa-83dc-4F88-951c-9d0782b4e376");

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [ComImport]
    [Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITipInvocation
    {
        void Toggle(IntPtr hwnd);
    }
}
