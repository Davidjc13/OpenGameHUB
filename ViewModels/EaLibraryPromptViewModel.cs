using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;
using OpenGameHUB.Providers.Ea;

namespace OpenGameHUB.ViewModels;

public partial class EaLibraryPromptViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;

    public EaLibraryPromptViewModel(SettingsService settingsService, EaLibraryCacheStatus cacheStatus)
    {
        _settingsService = settingsService;
        CacheStatus = cacheStatus;
        Strings = new LocalizedStrings();
    }

    public LocalizedStrings Strings { get; }

    public EaLibraryCacheStatus CacheStatus { get; }

    public bool IsLogFallback => CacheStatus == EaLibraryCacheStatus.DecryptFailedUsingLogs;

    public EaLibraryPromptChoice Choice { get; private set; }

    public event Action? RequestClose;

    [RelayCommand]
    private void Continue()
    {
        Choice = EaLibraryPromptChoice.Continue;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void OpenEaApp()
    {
        Choice = EaLibraryPromptChoice.OpenEaApp;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void DontRemindAgain()
    {
        Choice = EaLibraryPromptChoice.DontRemind;
        var settings = _settingsService.Current;
        settings.DismissEaLibraryPrompt = true;
        _settingsService.Save(settings);
        RequestClose?.Invoke();
    }
}
