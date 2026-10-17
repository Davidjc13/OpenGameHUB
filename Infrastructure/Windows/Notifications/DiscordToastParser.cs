using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Infrastructure.Windows.Notifications;

public static class DiscordToastParser
{
    private static readonly HashSet<string> KnownAppUserModelIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Discord.Discord",
        "Discord.DiscordPTB",
        "Discord.DiscordCanary",
        "com.squirrel.Discord.Discord",
        "com.squirrel.DiscordPTB.DiscordPTB",
        "com.squirrel.DiscordCanary.DiscordCanary"
    };

    public static bool IsDiscordApp(string? appUserModelId, string? displayName = null)
    {
        if (!string.IsNullOrWhiteSpace(appUserModelId))
        {
            var trimmed = appUserModelId.Trim();
            if (KnownAppUserModelIds.Contains(trimmed)
                || trimmed.Contains("Discord", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(displayName)
            && displayName.Contains("Discord", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static bool TryParse(
        uint id,
        DateTimeOffset receivedAt,
        IReadOnlyList<string?>? textElements,
        [NotNullWhen(true)] out DiscordNotificationMessage? message)
    {
        message = null;
        if (textElements is null || textElements.Count == 0)
            return false;

        var lines = new List<string>(textElements.Count);
        foreach (var raw in textElements)
        {
            var clean = NormalizeLine(raw);
            if (!string.IsNullOrEmpty(clean))
                lines.Add(clean);
        }

        if (lines.Count == 0)
            return false;

        string sender;
        string body;
        if (lines.Count == 1)
        {
            var single = lines[0];
            var colon = single.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0 && colon < single.Length - 2)
            {
                sender = single[..colon].Trim();
                body = single[(colon + 2)..].Trim();
            }
            else
            {
                sender = "Discord";
                body = single;
            }
        }
        else if (lines.Count == 2)
        {
            sender = lines[0];
            body = lines[1];
        }
        else
        {
            sender = $"{lines[0]} · {lines[1]}";
            body = string.Join(" ", lines.Skip(2));
        }

        if (string.IsNullOrWhiteSpace(body))
            return false;

        message = new DiscordNotificationMessage(
            Id: id,
            Sender: string.IsNullOrWhiteSpace(sender) ? "Discord" : sender,
            Body: body,
            ReceivedAt: receivedAt);
        return true;
    }

    public static bool TryParseXml(
        uint id,
        DateTimeOffset receivedAt,
        string? toastXml,
        [NotNullWhen(true)] out DiscordNotificationMessage? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(toastXml))
            return false;

        try
        {
            var doc = XDocument.Parse(toastXml);
            var texts = doc
                .Descendants()
                .Where(e => string.Equals(e.Name.LocalName, "text", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value)
                .ToList();

            return TryParse(id, receivedAt, texts, out message);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string NormalizeLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(
            " ",
            value
                .Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
