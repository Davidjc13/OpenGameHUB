using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Web.WebView2.Core;
using OpenGameHUB.Localization;
using OpenGameHUB.Services.Auth;
using OpenGameHUB.Views.EmbeddedBrowser;

namespace OpenGameHUB.ViewModels;

internal partial class EmbeddedBrowserViewModel : ViewModelBase
{
    private readonly IAuthCaptureStrategy _strategy;
    private readonly string _profilePath;
    private WebView2Host? _host;
    private bool _captureCompleted;

    public EmbeddedBrowserViewModel(IAuthCaptureStrategy strategy, string profilePath)
    {
        _strategy = strategy;
        _profilePath = profilePath;
        Strings = new LocalizedStrings();
        WindowTitle = Loc.T(strategy.WindowTitleKey);
        IntroText = Loc.T(strategy.IntroKey);
        StatusMessage = Loc.T("EmbeddedBrowserLoading");
    }

    public LocalizedStrings Strings { get; }

    public string WindowTitle { get; }

    public string IntroText { get; }

    [ObservableProperty]
    private string _statusMessage;

    [ObservableProperty]
    private bool _isBusy = true;

    public object? Result { get; private set; }

    public event Action? RequestClose;

    public void AttachHost(WebView2Host host)
    {
        if (_host is not null)
            return;

        _host = host;
        host.UserDataFolder = _profilePath;
        host.AllowedHosts = _strategy.AllowedHosts;
        host.NavigationStarting += OnNavigationStarting;
        host.AuthResponseReceived += OnAuthResponseReceived;
        _ = InitializeAsync();
    }

    public void DetachHost()
    {
        if (_host is null)
            return;

        _host.NavigationStarting -= OnNavigationStarting;
        _host.AuthResponseReceived -= OnAuthResponseReceived;
        _host = null;
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = null;
        RequestClose?.Invoke();
    }

    private async Task InitializeAsync()
    {
        if (_host is null)
            return;

        try
        {
            await _host.EnsureInitializedAsync().ConfigureAwait(true);
            await _host.NavigateAsync(_strategy.StartUrl).ConfigureAwait(true);
            StatusMessage = Loc.T("EmbeddedBrowserReady");
        }
        catch (Exception ex)
        {
            StatusMessage = Loc.T("EmbeddedBrowserInitFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnNavigationStarting(object? sender, string url)
    {
        if (_captureCompleted)
            return;

        var captured = _strategy.TryCaptureFromNavigation(url);
        if (captured is not null)
            CompleteCapture(captured);
    }

    private void OnAuthResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        if (_captureCompleted)
            return;

        _ = TryCaptureFromResponseAsync(e);
    }

    // Auth callbacks (OAuth code JSON, API key HTML) are tiny; cap the read so a large or hostile
    // response can never exhaust memory in the capture path.
    private const int MaxResponseBytes = 1024 * 1024;

    private async Task TryCaptureFromResponseAsync(CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        try
        {
            var response = e.Response;
            if (response is null || response.StatusCode < 200 || response.StatusCode >= 300)
                return;

            using var contentStream = await response.GetContentAsync().ConfigureAwait(true);
            if (contentStream is null)
                return;

            var body = await ReadCappedAsync(contentStream, MaxResponseBytes).ConfigureAwait(true);
            var captured = _strategy.TryCaptureFromResponse(e.Request.Uri, body);
            if (captured is not null)
                CompleteCapture(captured);
        }
        catch
        {
            // keep the browser open until the callback arrives
        }
    }

    private static async Task<string> ReadCappedAsync(Stream stream, int maxBytes)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while (buffer.Length < maxBytes && (read = await stream.ReadAsync(chunk).ConfigureAwait(true)) > 0)
        {
            var remaining = maxBytes - (int)buffer.Length;
            buffer.Write(chunk, 0, Math.Min(read, remaining));
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private void CompleteCapture(object captured)
    {
        if (_captureCompleted)
            return;

        if (captured is SteamBrowserCaptureResult steam && !steam.IsComplete)
        {
            StatusMessage = Loc.T("EmbeddedBrowserSteamWaitingForKey");
            return;
        }

        _captureCompleted = true;
        Result = captured;
        StatusMessage = Loc.T("EmbeddedBrowserCaptured");
        RequestClose?.Invoke();
    }
}
