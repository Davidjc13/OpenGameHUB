namespace OpenGameHUB.Services.Games;

internal interface ISteamRunningReader
{
    bool IsAppRunning(string appId);
}
