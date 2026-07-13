using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Localization;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.ViewModels;

public partial class AddCustomGameDialogViewModel : ViewModelBase
{
    private readonly GameLibraryService _libraryService;
    private readonly Func<Window> _getOwnerWindow;
    private readonly List<InstalledProgramItemViewModel> _allPrograms = [];

    public AddCustomGameDialogViewModel(GameLibraryService libraryService, Func<Window> getOwnerWindow)
    {
        _libraryService = libraryService;
        _getOwnerWindow = getOwnerWindow;
        FilteredPrograms = new ObservableCollection<InstalledProgramItemViewModel>();
        _ = LoadProgramsAsync();
    }

    public ObservableCollection<InstalledProgramItemViewModel> FilteredPrograms { get; }

    public LocalizedStrings Strings { get; } = new();

    public bool Confirmed { get; private set; }

    public UnifiedGame? CreatedGame { get; private set; }

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _gameTitle = string.Empty;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private InstalledProgramItemViewModel? _selectedProgram;

    [ObservableProperty]
    private bool _isLoadingPrograms = true;

    [ObservableProperty]
    private string? _validationMessage;

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnSelectedProgramChanged(InstalledProgramItemViewModel? value)
    {
        if (value is null)
            return;

        ExecutablePath = value.ExecutablePath;
        GameTitle = value.DisplayName;
        ValidationMessage = null;
    }

    [RelayCommand]
    private async Task BrowseExecutableAsync()
    {
        ValidationMessage = null;

        var files = await _getOwnerWindow().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("BrowseExecutableDialogTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc.T("ExecutableFilesFilter"))
                {
                    Patterns = ["*.exe"],
                    MimeTypes = ["application/octet-stream"]
                }
            ]
        });

        if (files.Count == 0)
            return;

        var localPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
            return;

        ExecutablePath = localPath;
        SelectedProgram = _allPrograms.FirstOrDefault(program =>
            string.Equals(program.ExecutablePath, localPath, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(GameTitle))
            GameTitle = Path.GetFileNameWithoutExtension(localPath);
    }

    [RelayCommand]
    private void Confirm(Window window)
    {
        ValidationMessage = null;

        if (string.IsNullOrWhiteSpace(ExecutablePath) || !File.Exists(ExecutablePath))
        {
            ValidationMessage = Loc.T("CustomGameInvalidExecutable");
            return;
        }

        if (string.IsNullOrWhiteSpace(GameTitle))
        {
            ValidationMessage = Loc.T("CustomGameTitleRequired");
            return;
        }

        if (_libraryService.CustomGames.Exists(ExecutablePath))
        {
            ValidationMessage = Loc.T("CustomGameAlreadyExists", GameTitle.Trim());
            return;
        }

        try
        {
            CreatedGame = _libraryService.AddCustomGame(GameTitle, ExecutablePath);
            Confirmed = true;
            window.Close();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel(Window window) => window.Close();

    private async Task LoadProgramsAsync()
    {
        IsLoadingPrograms = true;
        try
        {
            var entries = await Task.Run(InstalledProgramScanner.Scan);
            _allPrograms.Clear();
            _allPrograms.AddRange(entries.Select(entry => new InstalledProgramItemViewModel(entry)));
            ApplyFilter();
        }
        finally
        {
            IsLoadingPrograms = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredPrograms.Clear();
        var query = FilterText.Trim();

        IEnumerable<InstalledProgramItemViewModel> matches = _allPrograms;
        if (!string.IsNullOrWhiteSpace(query))
        {
            matches = _allPrograms.Where(program =>
                program.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || program.ExecutablePath.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var program in matches)
            FilteredPrograms.Add(program);
    }
}
