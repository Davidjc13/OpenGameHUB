using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Localization;

namespace OpenGameHUB.ViewModels;

public enum SteamApiKeyPromptChoice
{
    None,
    ContinueWithout,
    Configure,
    DontRemind
}

public partial class SteamApiKeyPromptViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;

    public SteamApiKeyPromptViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        Strings = new LocalizedStrings();
    }

    public LocalizedStrings Strings { get; }

    public SteamApiKeyPromptChoice Choice { get; private set; }

    public event Action? RequestClose;

    [RelayCommand]
    private void ContinueWithout()
    {
        Choice = SteamApiKeyPromptChoice.ContinueWithout;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Configure()
    {
        Choice = SteamApiKeyPromptChoice.Configure;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void DontRemindAgain()
    {
        Choice = SteamApiKeyPromptChoice.DontRemind;
        var current = _settingsService.Current;
        _settingsService.Save(new AppSettings
        {
            Language = current.Language,
            SteamApiKey = current.SteamApiKey,
            SteamId = current.SteamId,
            IgdbClientId = current.IgdbClientId,
            IgdbClientSecret = current.IgdbClientSecret,
            SteamGridDbApiKey = current.SteamGridDbApiKey,
            CoverQualityMode = current.CoverQualityMode,
            UiFontScale = current.UiFontScale,
            ThemeMode = current.ThemeMode,
            DismissSteamApiKeyPrompt = true,
            DismissEaLibraryPrompt = current.DismissEaLibraryPrompt,
            DismissLegendaryPrompt = current.DismissLegendaryPrompt,
            EpicAccountId = current.EpicAccountId,
            EpicDisplayName = current.EpicDisplayName
        });
        RequestClose?.Invoke();
    }
}
