using OpenGameHUB.Infrastructure.Windows.Notifications;

namespace OpenGameHUB.Tests;

public sealed class DiscordToastParserTests
{
    [Theory]
    [InlineData("Discord.Discord", null, true)]
    [InlineData("Discord.DiscordPTB", null, true)]
    [InlineData("Discord.DiscordCanary", null, true)]
    [InlineData("com.squirrel.Discord.Discord", null, true)]
    [InlineData("SomeVendor.DiscordBeta", null, true)]
    [InlineData("Unknown.App", "Discord", true)]
    [InlineData("Spotify.Spotify", "Spotify", false)]
    [InlineData("", null, false)]
    public void IsDiscordApp_matches_known_and_fallback_identifiers(
        string? aumid,
        string? displayName,
        bool expected)
    {
        Assert.Equal(expected, DiscordToastParser.IsDiscordApp(aumid, displayName));
    }

    [Fact]
    public void TryParse_extracts_sender_and_body_from_two_text_elements()
    {
        var now = DateTimeOffset.UtcNow;

        var ok = DiscordToastParser.TryParse(
            42,
            now,
            ["Alex", "¿Jugamos unas partidas?"],
            out var message);

        Assert.True(ok);
        Assert.NotNull(message);
        Assert.Equal(42u, message!.Id);
        Assert.Equal("Alex", message.Sender);
        Assert.Equal("¿Jugamos unas partidas?", message.Body);
        Assert.Equal(now, message.ReceivedAt);
    }

    [Fact]
    public void TryParse_combines_channel_and_sender_when_three_elements_exist()
    {
        var ok = DiscordToastParser.TryParse(
            7,
            DateTimeOffset.UtcNow,
            ["#general", "Alex", "Ya estoy dentro"],
            out var message);

        Assert.True(ok);
        Assert.Equal("#general · Alex", message!.Sender);
        Assert.Equal("Ya estoy dentro", message.Body);
    }

    [Fact]
    public void TryParseXml_reads_toast_generic_payload()
    {
        const string xml = """
            <toast>
              <visual>
                <binding template="ToastGeneric">
                  <text>Lucía</text>
                  <text>Conéctate al canal de voz</text>
                </binding>
              </visual>
            </toast>
            """;

        var ok = DiscordToastParser.TryParseXml(99, DateTimeOffset.UtcNow, xml, out var message);

        Assert.True(ok);
        Assert.Equal("Lucía", message!.Sender);
        Assert.Equal("Conéctate al canal de voz", message.Body);
    }
}
