using OpenGameHUB.Infrastructure.Browser;
using OpenGameHUB.Services.Configuration;

namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetAuthHelper
{
    public static void PersistProfile(SettingsService settings, string? displayName)
    {
        var current = settings.Current.Clone();
        current.BattleNetDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? Loc.T("BattleNetConnectedStatus")
            : displayName.Trim();
        settings.Save(current);
    }

    public static void Clear(SettingsService settings)
    {
        var current = settings.Current.Clone();
        current.BattleNetDisplayName = string.Empty;
        settings.Save(current);
        BattleNetLibraryCache.Clear();
        WebView2AuthProfile.ClearBattleNetProfile();
    }
}
