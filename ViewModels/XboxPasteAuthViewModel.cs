using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;
using OpenGameHUB.Services.Xbox;

namespace OpenGameHUB.ViewModels;

public partial class XboxPasteAuthViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _redirectUrl = string.Empty;

    public LocalizedStrings Strings { get; } = new();

    public string? AuthorizationCode { get; private set; }

    [RelayCommand]
    private void Confirm(Window window)
    {
        AuthorizationCode = XboxAuthService.TryExtractAuthorizationCode(RedirectUrl.Trim());
        window.Close();
    }

    [RelayCommand]
    private void Cancel(Window window) => window.Close();
}
