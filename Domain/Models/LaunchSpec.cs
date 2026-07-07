namespace OpenGameHUB.Domain.Models;

public sealed record LaunchSpec(string Kind, string Value)
{
    public static LaunchSpec None { get; } = new("none", string.Empty);

    public static LaunchSpec Protocol(string url) => new("protocol", url);

    public static LaunchSpec Executable(string path, string? arguments = null) =>
        new("executable", arguments is null ? path : $"{path}|{arguments}");

    public static LaunchSpec LauncherArgs(string launcherPath, string arguments) =>
        new("launcher-args", $"{launcherPath}|{arguments}");
}
