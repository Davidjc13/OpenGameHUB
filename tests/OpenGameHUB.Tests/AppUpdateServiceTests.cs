using OpenGameHUB.Services.Updates;

namespace OpenGameHUB.Tests;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public void IsNewer_returns_false_for_empty_latest_tag()
    {
        Assert.False(AppUpdateService.IsNewer("", "alpha-0.0.10"));
    }

    [Fact]
    public void IsNewer_returns_true_in_dev_build_when_latest_tag_is_present()
    {
        Assert.True(AppUpdateService.IsDevBuild);
        Assert.True(AppUpdateService.IsNewer("alpha-0.0.10", "alpha-0.0.10"));
    }

    [Fact]
    public void BuildUpdateHelperBatch_runs_installer_and_relaunches_from_app_directory()
    {
        var installer = @"C:\Temp\OpenGameHUB-Setup-alpha-0.0.11.exe";
        var appExe = @"C:\Users\test\AppData\Local\Programs\OpenGameHUB\OpenGameHUB.exe";

        var batch = AppUpdateService.BuildUpdateHelperBatch(installer, appExe);

        Assert.Contains($"\"{installer}\" /SILENT /CLOSEAPPLICATIONS", batch);
        Assert.Contains($"if exist \"{appExe}\" start \"\" /D \"C:\\Users\\test\\AppData\\Local\\Programs\\OpenGameHUB\" \"{appExe}\"", batch);
    }

    [Fact]
    public void BuildDetachedLaunchArguments_starts_helper_detached()
    {
        var helper = @"C:\Temp\OpenGameHUB\updates\apply-update.cmd";

        var arguments = AppUpdateService.BuildDetachedLaunchArguments(helper);

        Assert.Equal($"/c start \"\" /MIN \"{helper}\"", arguments);
    }
}
