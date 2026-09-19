using System.Diagnostics;

namespace OpenGameHUB.Infrastructure;

internal static class AppLog
{
    private static readonly object Sync = new();
    private static TextWriterTraceListener? _listener;
    private static string _logDirectory = AppPaths.LogDirectory;
    private static string _logFilePath = AppPaths.LogFilePath;

    internal static long MaxLogFileBytes { get; set; } = 2 * 1024 * 1024;

    public static string LogDirectory => _logDirectory;

    public static string LogFilePath => _logFilePath;

    public static void Initialize()
    {
        Initialize(AppPaths.LogDirectory, AppPaths.LogFilePath);
    }

    internal static void InitializeForTests(string logDirectory, string logFilePath)
    {
        Initialize(logDirectory, logFilePath);
    }

    internal static void ShutdownForTests()
    {
        lock (Sync)
        {
            if (_listener is null)
                return;

            Trace.Listeners.Remove(_listener);
            _listener.Dispose();
            _listener = null;
        }
    }

    internal static string ReadLogContentsForTests()
    {
        Flush();
        using var stream = new FileStream(
            _logFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static void Error(string message)
    {
        Write("ERROR", message);
    }

    public static void Fatal(string message, Exception? exception = null)
    {
        var body = exception is null
            ? message
            : $"{message}{Environment.NewLine}{exception}";
        Write("FATAL", body);
    }

    public static void Flush()
    {
        lock (Sync)
        {
            Trace.Flush();
            _listener?.Flush();
        }
    }

    public static void OpenLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = _logDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Trace.TraceError("Failed to open log directory: {0}", ex.Message);
        }
    }

    private static void Initialize(string logDirectory, string logFilePath)
    {
        lock (Sync)
        {
            _logDirectory = logDirectory;
            _logFilePath = logFilePath;
            Directory.CreateDirectory(logDirectory);
            RotateIfNeeded(logFilePath);

            if (_listener is not null)
            {
                Trace.Listeners.Remove(_listener);
                _listener.Dispose();
                _listener = null;
            }

            var stream = new FileStream(
                logFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
            var writer = new StreamWriter(stream) { AutoFlush = true };
            _listener = new TextWriterTraceListener(writer);
            Trace.Listeners.Add(_listener);
            Trace.AutoFlush = true;
        }
    }

    private static void Write(string level, string message)
    {
        lock (Sync)
        {
            RotateIfNeeded(_logFilePath);
            Trace.WriteLine($"{DateTime.UtcNow:O} [{level}] {message}");
        }
    }

    private static void RotateIfNeeded(string logFilePath)
    {
        if (!File.Exists(logFilePath))
            return;

        var info = new FileInfo(logFilePath);
        if (info.Length <= MaxLogFileBytes)
            return;

        var oldPath = logFilePath + ".old";
        if (File.Exists(oldPath))
            File.Delete(oldPath);

        File.Move(logFilePath, oldPath);
    }
}
