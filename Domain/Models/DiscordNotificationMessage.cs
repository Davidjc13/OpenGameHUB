namespace OpenGameHUB.Domain.Models;

public sealed record DiscordNotificationMessage(
    uint Id,
    string Sender,
    string Body,
    DateTimeOffset ReceivedAt,
    string? ReplyDraft = null);
