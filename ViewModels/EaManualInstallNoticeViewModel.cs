using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;

namespace OpenGameHUB.ViewModels;

public partial class EaManualInstallNoticeViewModel : ViewModelBase
{
    public EaManualInstallNoticeViewModel(string gameTitle)
    {
        GameTitle = gameTitle;
        Strings = new LocalizedStrings();
    }

    public LocalizedStrings Strings { get; }

    public string GameTitle { get; }

    public string Message => Loc.T("EaManualInstallNotice");

    public event Action? RequestClose;

    [RelayCommand]
    private void Close() => RequestClose?.Invoke();
}
