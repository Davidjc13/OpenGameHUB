using System.Diagnostics;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Services.Games;

public sealed class GameLaunchNotification : EventArgs
{
    public required UnifiedGame Game { get; init; }

    public required bool IsInstall { get; init; }

    public Process? Process { get; init; }
}
