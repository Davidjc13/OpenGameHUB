namespace OpenGameHUB.Services.Games;

internal interface IGameProcessCatalog
{
    IReadOnlyList<GameProcessInfo> Snapshot();
}
