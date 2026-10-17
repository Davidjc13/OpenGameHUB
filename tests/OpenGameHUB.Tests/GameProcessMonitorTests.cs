using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.Tests;

public sealed class GameProcessMonitorTests
{
    private static readonly string InstallPath = Path.Combine(
        Path.GetTempPath(),
        "ogh-play-session",
        "Hades");

    [Fact]
    public void Launched_game_exe_under_install_path_is_playing()
    {
        var launched = GameExe(401, "Hades");
        var catalog = new FakeCatalog(launched);
        var monitor = CreateMonitor(catalog, launched);

        Assert.Equal(GameMonitorPhase.Playing, monitor.Evaluate());
        Assert.True(monitor.IsGameRunning());
    }

    [Fact]
    public void Steam_exe_is_not_the_game()
    {
        var steam = new GameProcessInfo(
            88,
            Path.Combine(InstallPath, "steam.exe"),
            "steam");
        var catalog = new FakeCatalog(steam);
        var now = new DateTime(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
        var monitor = CreateMonitor(catalog, steam, utcNow: () => now);

        Assert.Equal(GameMonitorPhase.Acquiring, monitor.Evaluate());
        Assert.False(monitor.IsGameRunning());
    }

    [Fact]
    public void Process_under_install_path_is_playing()
    {
        var catalog = new FakeCatalog(GameExe(12, "Hades"));
        var monitor = CreateMonitor(catalog, launched: null);

        Assert.Equal(GameMonitorPhase.Playing, monitor.Evaluate());
    }

    [Fact]
    public void Known_launcher_under_install_path_is_ignored()
    {
        var catalog = new FakeCatalog(new GameProcessInfo(
            9,
            Path.Combine(InstallPath, "EpicGamesLauncher.exe"),
            "EpicGamesLauncher"));
        var now = new DateTime(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
        var monitor = CreateMonitor(catalog, launched: null, utcNow: () => now);

        Assert.False(GameProcessMatcher.IsGameProcess(catalog.Processes[0], InstallPath));
        Assert.Equal(GameMonitorPhase.Acquiring, monitor.Evaluate());
    }

    [Fact]
    public void Acquire_timeout_ends_session_when_no_process()
    {
        var catalog = new FakeCatalog();
        var now = new DateTime(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
        var monitor = CreateMonitor(catalog, launched: null, utcNow: () => now);

        Assert.Equal(GameMonitorPhase.Acquiring, monitor.Evaluate());

        now += GameProcessMonitor.AcquireTimeout;
        Assert.Equal(GameMonitorPhase.Ended, monitor.Evaluate());
    }

    [Fact]
    public void Debounce_keeps_playing_then_ends()
    {
        var catalog = new FakeCatalog(GameExe(21, "Hades"));
        var now = new DateTime(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
        var monitor = CreateMonitor(catalog, launched: null, utcNow: () => now);

        Assert.Equal(GameMonitorPhase.Playing, monitor.Evaluate());

        catalog.Processes.Clear();
        now += TimeSpan.FromSeconds(1);
        Assert.Equal(GameMonitorPhase.Playing, monitor.Evaluate());

        now += GameProcessMonitor.EmptyDebounce;
        Assert.Equal(GameMonitorPhase.Ended, monitor.Evaluate());
    }

    [Fact]
    public void Steam_running_flag_counts_as_playing()
    {
        var steam = new FakeSteam { Running = true };
        var now = new DateTime(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
        var monitor = CreateMonitor(
            new FakeCatalog(),
            launched: null,
            platform: Platform.Steam,
            steam: steam,
            utcNow: () => now);

        Assert.Equal(GameMonitorPhase.Playing, monitor.Evaluate());

        steam.Running = false;
        now += GameProcessMonitor.EmptyDebounce;
        Assert.Equal(GameMonitorPhase.Ended, monitor.Evaluate());
    }

    [Fact]
    public void Launched_process_that_exits_is_no_longer_running()
    {
        var launched = GameExe(77, "Hades");
        var catalog = new FakeCatalog(launched);
        var now = new DateTime(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
        var monitor = CreateMonitor(catalog, launched, utcNow: () => now);

        Assert.Equal(GameMonitorPhase.Playing, monitor.Evaluate());

        catalog.Processes.Clear();
        now += GameProcessMonitor.EmptyDebounce;
        Assert.Equal(GameMonitorPhase.Ended, monitor.Evaluate());
    }

    [Theory]
    [InlineData("steam")]
    [InlineData("steam.exe")]
    [InlineData(@"C:\Program Files (x86)\Steam\steam.exe")]
    [InlineData("EpicGamesLauncher")]
    [InlineData("OpenGameHUB")]
    public void Known_launchers_are_detected(string name) =>
        Assert.True(GameProcessMatcher.IsKnownLauncher(name));

    [Theory]
    [InlineData("Hades")]
    [InlineData("game")]
    [InlineData(null)]
    [InlineData("")]
    public void Game_executables_are_not_launchers(string? name) =>
        Assert.False(GameProcessMatcher.IsKnownLauncher(name));

    [Fact]
    public void Install_path_matches_nested_exe()
    {
        var exe = Path.Combine(InstallPath, "Binaries", "Hades.exe");
        Assert.True(GameProcessMatcher.IsUnderInstallPath(exe, InstallPath));
        Assert.False(GameProcessMatcher.IsUnderInstallPath(
            Path.Combine(Path.GetTempPath(), "other", "Hades.exe"),
            InstallPath));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(2L, true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData(null, false)]
    public void Steam_running_values_are_parsed(object? value, bool expected) =>
        Assert.Equal(expected, SteamRunningAppReader.IsRunningValue(value));

    private static GameProcessMonitor CreateMonitor(
        FakeCatalog catalog,
        GameProcessInfo? launched,
        Platform platform = Platform.Custom,
        ISteamRunningReader? steam = null,
        Func<DateTime>? utcNow = null)
    {
        var game = TestGames.Create(
            "custom:hades",
            platform,
            "Hades",
            installed: true,
            installPath: InstallPath,
            platformGameId: "1145360");

        return new GameProcessMonitor(
            game,
            launched,
            catalog,
            steam ?? new FakeSteam(),
            utcNow);
    }

    private static GameProcessInfo GameExe(int id, string name) =>
        new(id, Path.Combine(InstallPath, name + ".exe"), name);

    private sealed class FakeCatalog : IGameProcessCatalog
    {
        public FakeCatalog(params GameProcessInfo[] processes)
        {
            Processes.AddRange(processes);
        }

        public List<GameProcessInfo> Processes { get; } = [];

        public IReadOnlyList<GameProcessInfo> Snapshot() => Processes.ToArray();
    }

    private sealed class FakeSteam : ISteamRunningReader
    {
        public bool Running { get; set; }

        public bool IsAppRunning(string appId) => Running;
    }
}
