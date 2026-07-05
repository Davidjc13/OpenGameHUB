namespace OpenGameHUB.Models;

public static class PlatformLabels
{
    public static string Get(Platform platform) => platform switch
    {
        Platform.Ea => "EA",
        Platform.BattleNet => "Battle.net",
        Platform.GamePass => "Game Pass",
        _ => platform.ToString()
    };
}
