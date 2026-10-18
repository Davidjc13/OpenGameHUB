using System.Text.Json;

namespace OpenGameHUB.Infrastructure;

internal static class DebugAgentLog
{
    private const string LogPath = @"c:\Users\david\OpenGameHUB\debug-80195f.log";
    private static readonly object Gate = new();

    public static void Write(
        string hypothesisId,
        string location,
        string message,
        object? data = null,
        string runId = "post-fix")
    {
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                sessionId = "80195f",
                runId,
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            lock (Gate)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // debug-only
        }
    }
}
