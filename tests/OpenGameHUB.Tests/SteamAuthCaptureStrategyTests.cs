using OpenGameHUB.Services.Auth;

namespace OpenGameHUB.Tests;

public sealed class SteamAuthCaptureStrategyTests
{
    private const string SampleApiKeyPage = """
        <html>
        <script>var g_steamID = "76561198123456789";</script>
        <body>
        <input type="text" readonly value="ABCDEF0123456789ABCDEF0123456789" />
        </body>
        </html>
        """;

    [Fact]
    public void TryParseSteamId_reads_g_steamID_variable()
    {
        var steamId = SteamAuthCaptureStrategy.TryParseSteamId(
            """<script>g_steamID = "76561198123456789";</script>""");

        Assert.Equal("76561198123456789", steamId);
    }

    [Fact]
    public void TryParseApiKey_reads_input_value()
    {
        var apiKey = SteamAuthCaptureStrategy.TryParseApiKey(
            """<input type="text" value="ABCDEF0123456789ABCDEF0123456789" />""");

        Assert.Equal("ABCDEF0123456789ABCDEF0123456789", apiKey);
    }

    [Fact]
    public void TryCaptureFromResponse_accumulates_steam_id_and_api_key()
    {
        var strategy = new SteamAuthCaptureStrategy();

        var partial = strategy.TryCaptureFromResponse(
            "https://steamcommunity.com/profiles/76561198123456789/home",
            """<script>g_steamID = "76561198123456789";</script>""");

        var result = partial as SteamBrowserCaptureResult;
        Assert.NotNull(result);
        Assert.Equal("76561198123456789", result.SteamId);
        Assert.Null(result.ApiKey);
        Assert.False(result.IsComplete);

        var complete = strategy.TryCaptureFromResponse(
            "https://steamcommunity.com/dev/apikey",
            SampleApiKeyPage) as SteamBrowserCaptureResult;

        Assert.NotNull(complete);
        Assert.Equal("76561198123456789", complete.SteamId);
        Assert.Equal("ABCDEF0123456789ABCDEF0123456789", complete.ApiKey);
        Assert.True(complete.IsComplete);
    }
}
