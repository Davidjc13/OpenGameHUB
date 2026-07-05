using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;
using OpenGameHUB.Models;
using OpenGameHUB.Services;

namespace OpenGameHUB.ViewModels;

public partial class LegendaryPromptViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;

    public LegendaryPromptViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        Strings = new LocalizedStrings();
    }

    public LocalizedStrings Strings { get; }

    public LegendaryPromptChoice Choice { get; private set; }

    public event Action? RequestClose;

    [RelayCommand]
    private void ConnectEpic()
    {
        Choice = LegendaryPromptChoice.ConnectEpic;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Continue()
    {
        Choice = LegendaryPromptChoice.Continue;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void OpenGuide()
    {
        Choice = LegendaryPromptChoice.OpenGuide;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void DontRemindAgain()
    {
        Choice = LegendaryPromptChoice.DontRemind;
        var current = _settingsService.Current;
        current.DismissLegendaryPrompt = true;
        _settingsService.Save(current);
        RequestClose?.Invoke();
    }
}
