using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Tests;

public sealed class LaunchSpecTests
{
    [Fact]
    public void LauncherArgs_encodes_executable_and_arguments_with_pipe_separator()
    {
        var spec = LaunchSpec.LauncherArgs(
            @"C:\Riot Games\Riot Client\RiotClientServices.exe",
            "--launch-product=valorant --skip-to-install");

        Assert.Equal("launcher-args", spec.Kind);
        Assert.StartsWith(@"C:\Riot Games\Riot Client\RiotClientServices.exe|", spec.Value);
        Assert.Contains("--skip-to-install", spec.Value);
    }

    [Fact]
    public void Executable_with_arguments_uses_pipe_separator()
    {
        var spec = LaunchSpec.Executable(@"C:\Games\foo.exe", "--fullscreen");

        Assert.Equal("executable", spec.Kind);
        Assert.Equal(@"C:\Games\foo.exe|--fullscreen", spec.Value);
    }
}
