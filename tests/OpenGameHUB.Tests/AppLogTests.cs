using OpenGameHUB.Domain.Enums;
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
        try
        {
            AppLog.Initialize();
        }
        catch (IOException)
        {
            // Production log may be held by a running OpenGameHUB instance.
        }

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
    public void ReportError_with_platform_writes_to_platform_log_file()
    {
        var exception = new InvalidOperationException("platform boom");

        AppDiagnostics.ReportError("AppLogTests", "PlatformOp", exception, Platform.BattleNet);

        var platformLogPath = AppLog.GetPlatformLogFilePath(Platform.BattleNet);
        Assert.True(File.Exists(platformLogPath));
        var platformContents = File.ReadAllText(platformLogPath);
        Assert.Contains("[ERROR]", platformContents);
        Assert.Contains("platform boom", platformContents);
        Assert.Contains("operation=PlatformOp", platformContents);
        Assert.DoesNotContain("platform boom", AppLog.ReadLogContentsForTests());
    }

    [Fact]
    public void ReportInfo_writes_to_platform_log_folder()
    {
        AppDiagnostics.ReportInfo("AppLogTests", "SyncSteam", Platform.Steam, "ownedCount=3");

        var path = AppLog.GetPlatformLogFilePath(Platform.Steam);
        Assert.Equal(Path.Combine(_tempDirectory, "Steam", "steam.log"), path);
        Assert.True(File.Exists(path));
        var contents = File.ReadAllText(path);
        Assert.Contains("[Information]", contents);
        Assert.Contains("operation=SyncSteam", contents);
        Assert.Contains("ownedCount=3", contents);
        Assert.DoesNotContain("ownedCount=3", AppLog.ReadLogContentsForTests());
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
