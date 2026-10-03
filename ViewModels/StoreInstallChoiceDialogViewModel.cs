using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Localization;

namespace OpenGameHUB.ViewModels;

public sealed partial class StoreInstallChoiceDialogViewModel : ViewModelBase
{
    public StoreInstallChoiceDialogViewModel(string gameTitle, IReadOnlyList<UnifiedGame> options)
    {
        GameTitle = gameTitle;
        Options = new ObservableCollection<StoreInstallOption>(
            options.Select(g => new StoreInstallOption(g)));
        SelectedOption = Options.FirstOrDefault();
    }

    public string GameTitle { get; }

    public string Title => Loc.T("ChooseInstallStoreTitle");

    public string Message => Loc.T("ChooseInstallStoreMessage", GameTitle);

    public ObservableCollection<StoreInstallOption> Options { get; }

    [ObservableProperty]
    private StoreInstallOption? _selectedOption;

    public bool Confirmed { get; private set; }

    public UnifiedGame? SelectedGame { get; private set; }

    public LocalizedStrings Strings { get; } = new();

    public string InstallLabel => Loc.T("Install");

    [RelayCommand]
    private void Confirm(Window window)
    {
        if (SelectedOption is null)
            return;

        Confirmed = true;
        SelectedGame = SelectedOption.Game;
        window.Close();
    }

    [RelayCommand]
    private void Cancel(Window window) => window.Close();
}

public sealed class StoreInstallOption
{
    public StoreInstallOption(UnifiedGame game)
    {
        Game = game;
        PlatformLabel = game.PlatformLabel;
    }

    public UnifiedGame Game { get; }

    public string PlatformLabel { get; }

    public override string ToString() => PlatformLabel;
}
