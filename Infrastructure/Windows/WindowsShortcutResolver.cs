using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace OpenGameHUB.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsShortcutResolver
{
    public static string? ResolveTargetPath(string shortcutPath)
    {
        if (!File.Exists(shortcutPath))
            return null;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
                return null;

            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                try
                {
                    return string.IsNullOrWhiteSpace(shortcut.TargetPath)
                        ? null
                        : (string)shortcut.TargetPath;
                }
                finally
                {
                    Marshal.ReleaseComObject(shortcut);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }
        catch
        {
            return null;
        }
    }
}
