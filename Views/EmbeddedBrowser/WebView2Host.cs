using System.Drawing;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;
using OpenGameHUB.Infrastructure.Browser;

namespace OpenGameHUB.Views.EmbeddedBrowser;

public sealed class WebView2Host : NativeControlHost, IDisposable
{
    private const int WsChild = 0x40000000;
    private const int WsVisible = 0x10000000;

    private IntPtr _hostHwnd;
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _webView;
    private readonly TaskCompletionSource<bool> _initTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _initStarted;
    private string? _userDataFolder;
    private bool _disposed;

    public event EventHandler<string>? NavigationStarting;
    public event EventHandler<CoreWebView2WebResourceResponseReceivedEventArgs>? AuthResponseReceived;

    // Setting the profile is one of the two preconditions to start WebView2; the other is the
    // native host window. Whichever arrives last kicks off initialization, so order is irrelevant.
    public string? UserDataFolder
    {
        get => _userDataFolder;
        set
        {
            _userDataFolder = value;
            TryStartInitialization();
        }
    }

    public IReadOnlyList<string> AllowedHosts { get; set; } = [];

    public bool IsReady => _webView is not null;

    public string? CurrentSource => _webView?.Source;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_webView is not null)
            return;

        TryStartInitialization();

        using var registration = cancellationToken.Register(() => _initTcs.TrySetCanceled(cancellationToken));
        await _initTcs.Task.ConfigureAwait(true);
    }

    public async Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(true);
        _webView!.Navigate(url);
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _hostHwnd = CreateHostWindow(parent.Handle);
        TryStartInitialization();
        return new PlatformHandle(_hostHwnd, "HWND");
    }

    private void TryStartInitialization()
    {
        if (_initStarted || _disposed)
            return;

        if (_hostHwnd == IntPtr.Zero || string.IsNullOrWhiteSpace(_userDataFolder))
            return;

        _initStarted = true;
        _ = InitializeAsync();
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control) => Dispose();

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBounds();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
            UpdateBounds();
    }

    private static IntPtr CreateHostWindow(IntPtr parent)
    {
        var hwnd = CreateWindowEx(
            0,
            "Static",
            string.Empty,
            WsChild | WsVisible,
            0,
            0,
            1,
            1,
            parent,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create WebView2 host window.");

        return hwnd;
    }

    private async Task InitializeAsync()
    {
        try
        {
            var userDataFolder = _userDataFolder
                ?? throw new InvalidOperationException("WebView2 auth profile path is required.");

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder).ConfigureAwait(true);

            var controller = await environment.CreateCoreWebView2ControllerAsync(_hostHwnd).ConfigureAwait(true);
            _controller = controller;
            _webView = controller.CoreWebView2;

            // Auth browser must never call AddHostObjectToScript; pages cannot reach .NET.
            // Login-only surface: no devtools, downloads, pop-ups, or accelerator keys.
            _webView.Settings.AreDefaultScriptDialogsEnabled = true;
            _webView.Settings.IsStatusBarEnabled = false;
            _webView.Settings.AreDevToolsEnabled = false;
            _webView.Settings.AreDefaultContextMenusEnabled = false;
            _webView.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _webView.Settings.IsBuiltInErrorPageEnabled = false;
            _webView.Settings.IsZoomControlEnabled = false;

            _webView.NavigationStarting += (_, e) =>
            {
                // Only absolute HTTPS URLs whose host is on the provider allowlist may load.
                if (!AuthUrl.TryParse(e.Uri, out var uri) || !AuthHostPolicy.IsHostAllowed(uri.Host, AllowedHosts))
                {
                    e.Cancel = true;
                    return;
                }

                NavigationStarting?.Invoke(this, e.Uri);
            };

            _webView.WebResourceResponseReceived += (_, e) =>
            {
                if (!AuthUrl.TryParse(e.Request.Uri, out var uri) || !AuthHostPolicy.IsHostAllowed(uri.Host, AllowedHosts))
                    return;

                AuthResponseReceived?.Invoke(this, e);
            };

            _webView.NewWindowRequested += (_, e) => e.Handled = true;
            _webView.DownloadStarting += (_, e) => e.Cancel = true;

            UpdateBounds();
            _initTcs.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _initTcs.TrySetException(ex);
        }
    }

    private void UpdateBounds()
    {
        if (_controller is null)
            return;

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var width = Math.Max(1, (int)(Bounds.Width * scaling));
        var height = Math.Max(1, (int)(Bounds.Height * scaling));
        _controller.Bounds = new Rectangle(0, 0, width, height);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _initTcs.TrySetCanceled();
        _controller?.Close();
        _controller = null;
        _webView = null;

        if (_hostHwnd != IntPtr.Zero)
        {
            DestroyWindow(_hostHwnd);
            _hostHwnd = IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);
}
