using System;
using Avalonia;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppLog.Initialize();
        AppCrashHandlers.RegisterGlobalHandlers();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLog.Fatal("Startup failed", ex);
            AppLog.Flush();
            AppCrashHandlers.ShowFatalDialog(ex);
        }
        finally
        {
            AppLog.Flush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(CreateWin32Options())
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    internal static bool UseSoftwareRendering =>
        string.Equals(
            Environment.GetEnvironmentVariable("OPENGAMEHUB_RENDER_SOFTWARE"),
            "1",
            StringComparison.Ordinal);

    internal static bool UseLowMemoryComposition =>
        UseSoftwareRendering
        || string.Equals(
            Environment.GetEnvironmentVariable("OPENGAMEHUB_LOW_MEMORY"),
            "1",
            StringComparison.Ordinal);

    private static Win32PlatformOptions CreateWin32Options()
    {
        var options = new Win32PlatformOptions
        {
            RenderingMode = UseSoftwareRendering
                ? [Win32RenderingMode.Software]
                : [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software]
        };

        if (UseLowMemoryComposition)
            options.CompositionMode = [Win32CompositionMode.RedirectionSurface];

        return options;
    }
}
