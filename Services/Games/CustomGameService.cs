using System.Security.Cryptography;
using System.Text;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Database;

namespace OpenGameHUB.Services.Games;

public sealed class CustomGameService
{
    private readonly GameDatabase _database;

    public CustomGameService(GameDatabase database) => _database = database;

    public IReadOnlyList<UnifiedGame> LoadAll() =>
        _database.GetAllGames()
            .Where(game => game.Id.StartsWith("custom:", StringComparison.Ordinal))
            .ToList();

    public bool Exists(string executablePath)
    {
        var id = CreateId(NormalizeExecutablePath(executablePath));
        return _database.GetAllGames().Any(game => game.Id == id);
    }

    public UnifiedGame Add(string title, string executablePath)
    {
        var normalizedPath = NormalizeExecutablePath(executablePath);
        if (!File.Exists(normalizedPath))
            throw new FileNotFoundException(Loc.T("ExecutableNotFound", normalizedPath), normalizedPath);

        var trimmedTitle = title.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle))
            throw new ArgumentException(Loc.T("CustomGameTitleRequired"), nameof(title));

        var id = CreateId(normalizedPath);
        if (_database.GetAllGames().Any(game => game.Id == id))
            throw new InvalidOperationException(Loc.T("CustomGameAlreadyExists", trimmedTitle));

        var installPath = Path.GetDirectoryName(normalizedPath);
        var game = new UnifiedGame
        {
            Id = id,
            Platform = Platform.Custom,
            PlatformGameId = Path.GetFileName(normalizedPath),
            Title = trimmedTitle,
            IsInstalled = true,
            InstallPath = installPath,
            LaunchSpec = LaunchSpec.Executable(normalizedPath)
        };

        _database.UpsertGames([game]);
        return game;
    }

    public static string CreateId(string normalizedExecutablePath)
    {
        var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalizedExecutablePath.ToLowerInvariant())))
            [..16];
        return $"custom:path:{hash}";
    }

    private static string NormalizeExecutablePath(string executablePath) =>
        Path.GetFullPath(executablePath.Trim('"'));
}
