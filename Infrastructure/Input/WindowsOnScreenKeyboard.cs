using System.Diagnostics;

namespace OpenGameHUB.Infrastructure.Input;

public static class WindowsOnScreenKeyboard
{
    public static bool TryShow()
    {
        foreach (var path in TabTipPaths())
        {
            if (TryStart(path))
                return true;
        }

        return TryStart("osk.exe");
    }

    private static IEnumerable<string> TabTipPaths()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            @"Microsoft Shared\ink\TabTip.exe");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            @"Common Files\Microsoft Shared\ink\TabTip.exe");
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
}
