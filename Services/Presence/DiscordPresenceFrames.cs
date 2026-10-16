using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGameHUB.Services.Presence;

public sealed record DiscordActivity(string Details, string State);

public interface IDiscordPresence
{
    void Publish(string? applicationId, DiscordActivity? activity);
}

public static class DiscordPresenceFrames
{
    public const int HandshakeOp = 0;
    public const int FrameOp = 1;

    public static bool IsApplicationId(string? value)
    {
        var id = value?.Trim() ?? string.Empty;
        return id.Length is >= 17 and <= 20 && id.All(char.IsDigit);
    }

    public static string Handshake(string applicationId) =>
        JsonSerializer.Serialize(new HandshakeBody(applicationId.Trim()));

    public static string SetActivity(int processId, string nonce, DiscordActivity activity) =>
        JsonSerializer.Serialize(new CommandBody(
            "SET_ACTIVITY",
            nonce,
            new SetActivityArgs(
                processId,
                new ActivityBody(Truncate(activity.Details), Truncate(activity.State)))));

    public static string ClearActivity(int processId, string nonce) =>
        JsonSerializer.Serialize(new CommandBody(
            "SET_ACTIVITY",
            nonce,
            new ClearActivityArgs(processId)));

    private static string Truncate(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 128 ? trimmed : trimmed[..128];
    }

    private sealed record HandshakeBody(
        [property: JsonPropertyName("client_id")] string ClientId)
    {
        [JsonPropertyName("v")]
        public int Version { get; } = 1;
    }

    private sealed record CommandBody(
        [property: JsonPropertyName("cmd")] string Command,
        [property: JsonPropertyName("nonce")] string Nonce,
        [property: JsonPropertyName("args")] object Args);

    private sealed record SetActivityArgs(
        [property: JsonPropertyName("pid")] int ProcessId,
        [property: JsonPropertyName("activity")] ActivityBody Activity);

    private sealed record ActivityBody(
        [property: JsonPropertyName("details")] string Details,
        [property: JsonPropertyName("state")] string State)
    {
        [JsonPropertyName("assets")]
        public ActivityAssets Assets { get; } = new();

        [JsonPropertyName("instance")]
        public bool Instance { get; }
    }

    private sealed record ActivityAssets
    {
        [JsonPropertyName("large_text")]
        public string LargeText { get; } = "OpenGameHUB";
    }

    private sealed class ClearActivityArgs
    {
        public ClearActivityArgs(int processId) => ProcessId = processId;

        [JsonPropertyName("pid")]
        public int ProcessId { get; }

        [JsonPropertyName("activity")]
        public object? Activity => null;
    }
}
