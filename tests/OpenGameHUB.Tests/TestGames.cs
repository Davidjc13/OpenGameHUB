using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Tests;

internal static class TestGames
{
    public static UnifiedGame Create(
        string id,
        Platform platform,
        string title,
        bool installed = false,
        string? installPath = null,
        string? platformGameId = null)
    {
        return new UnifiedGame
        {
            Id = id,
            Platform = platform,
            PlatformGameId = platformGameId ?? id.Split(':').Last(),
            Title = title,
            IsInstalled = installed,
            InstallPath = installPath,
            LaunchSpec = LaunchSpec.Protocol("test://launch")
        };
    }
}
