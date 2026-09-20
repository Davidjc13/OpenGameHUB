using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.ViewModels;

internal static class GameItemViewModelFactory
{
    public static List<GameItemViewModel> CreateGrouped(IReadOnlyList<UnifiedGame> games) =>
        GameLibraryGrouper.GroupByTitle(games)
            .Select(g => new GameItemViewModel(g))
            .ToList();
}
