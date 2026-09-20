using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Tests;

public sealed class AppLogTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"ogh-log-{Guid.NewGuid():N}");
    private readonly string _logFilePath;
    private readonly long _originalMaxLogFileBytes;

    public AppLogTests()
    {
        _logFilePath = Path.Combine(_tempDirectory, "app.log");
        _originalMaxLogFileBytes = AppLog.MaxLogFileBytes;
        Directory.CreateDirectory(_tempDirectory);
        AppLog.InitializeForTests(_tempDirectory, _logFilePath);
    }

    public void Dispose()
    {
        AppLog.ShutdownForTests();
        AppLog.MaxLogFileBytes = _originalMaxLogFileBytes;
        AppLog.Initialize();

        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Best effort cleanup for temp test files.
        }
    }

    [Fact]
    public void Error_writes_timestamp_message_and_stack_trace_to_log_file()
    {
        var exception = new InvalidOperationException("boom");

        AppDiagnostics.ReportError("AppLogTests", "Test", exception);

        var contents = AppLog.ReadLogContentsForTests();
        Assert.Contains("[ERROR]", contents);
        Assert.Contains("InvalidOperationException", contents);
        Assert.Contains("boom", contents);
        Assert.Contains("operation=Test", contents);
    }

    [Fact]
    public void Fatal_writes_fatal_level_to_log_file()
    {
        AppLog.Fatal("fatal test", new ArgumentException("bad arg"));

        var contents = AppLog.ReadLogContentsForTests();
        Assert.Contains("[FATAL]", contents);
        Assert.Contains("fatal test", contents);
        Assert.Contains("ArgumentException", contents);
    }

    [Fact]
    public void RotateIfNeeded_renames_log_when_size_exceeds_threshold()
    {
        AppLog.MaxLogFileBytes = 64;
        AppLog.ShutdownForTests();
        File.WriteAllText(_logFilePath, new string('x', 128));
        AppLog.InitializeForTests(_tempDirectory, _logFilePath);

        AppLog.Error("after rotation");

        Assert.True(File.Exists(_logFilePath));
        Assert.True(File.Exists(_logFilePath + ".old"));
        Assert.Contains("after rotation", AppLog.ReadLogContentsForTests());
    }
}
