using OpenGameHUB.Models;

namespace OpenGameHUB.Services.Epic;

internal static class EpicAuthHelper
{
    public static void PersistFromLegendary(SettingsService settings)
    {
        if (!LegendaryClient.HasStoredCredentials())
            return;

        var accountId = LegendaryClient.GetAccountId();
        if (string.IsNullOrWhiteSpace(accountId))
            return;

        var displayName = LegendaryClient.GetDisplayName() ?? string.Empty;
        var current = settings.Current.Clone();
        if (current.EpicAccountId == accountId && current.EpicDisplayName == displayName)
            return;

        current.EpicAccountId = accountId;
        current.EpicDisplayName = displayName;
        settings.Save(current);
    }

    public static void Clear(SettingsService settings)
    {
        var current = settings.Current;
        if (string.IsNullOrWhiteSpace(current.EpicAccountId) && string.IsNullOrWhiteSpace(current.EpicDisplayName))
            return;

        var cleared = current.Clone();
        cleared.EpicAccountId = string.Empty;
        cleared.EpicDisplayName = string.Empty;
        settings.Save(cleared);
    }
}
