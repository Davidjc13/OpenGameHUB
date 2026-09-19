using CommunityToolkit.Mvvm.ComponentModel;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.ViewModels;

public sealed partial class InstalledProgramItemViewModel : ObservableObject
{
    public InstalledProgramItemViewModel(InstalledProgramEntry entry)
    {
        DisplayName = entry.DisplayName;
        ExecutablePath = entry.ExecutablePath;
    }

    public string DisplayName { get; }

    public string ExecutablePath { get; }

    public string Label => $"{DisplayName} — {ExecutablePath}";
}
