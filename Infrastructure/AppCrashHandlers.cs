using System.Runtime.InteropServices;
using Avalonia.Threading;
using OpenGameHUB.Services.Configuration;

namespace OpenGameHUB.Infrastructure;

internal static class AppCrashHandlers
{
    private static int _globalRegistered;
    private static int _uiRegistered;

    private const uint MbOk = 0x00000000;
    private const uint MbIconError = 0x00000010;

    public static void RegisterGlobalHandlers()
    {
        if (Interlocked.Exchange(ref _globalRegistered, 1) == 1)
            return;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static void RegisterUiThreadHandler()
    {
        if (Interlocked.Exchange(ref _uiRegistered, 1) == 1)
            return;

        Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;
    }

    public static void ShowFatalDialog(Exception exception)
    {
        var message = Loc.T("FatalCrashMessage", AppLog.LogFilePath, exception.Message);
        var title = Loc.T("FatalCrashTitle");
        MessageBoxW(IntPtr.Zero, message, title, MbOk | MbIconError);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception
            ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown unhandled exception");

        AppLog.Fatal("Unhandled AppDomain exception", exception);
        AppLog.Flush();

        if (e.IsTerminating)
            ShowFatalDialog(exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLog.Fatal("Unobserved task exception", e.Exception);
        e.SetObserved();
    }

    private static void OnUiThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Fatal("Unhandled UI thread exception", e.Exception);
        AppLog.Flush();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
