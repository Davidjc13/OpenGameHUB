using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Xbox;

internal static class XboxAuthHelper
{
    public static void PersistProfile(SettingsService settings, string? gamertag)
    {
        if (string.IsNullOrWhiteSpace(gamertag))
            return;

        var current = settings.Current.Clone();
        if (string.Equals(current.XboxGamertag, gamertag, StringComparison.Ordinal))
            return;

        current.XboxGamertag = gamertag;
        settings.Save(current);
    }

    public static void Clear(SettingsService settings)
    {
        XboxAccountClient.SignOut();

        var current = settings.Current;
        if (string.IsNullOrWhiteSpace(current.XboxGamertag))
            return;

        var cleared = current.Clone();
        cleared.XboxGamertag = string.Empty;
        settings.Save(cleared);
    }
}
