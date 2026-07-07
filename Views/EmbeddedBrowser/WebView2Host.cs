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
    private TaskCompletionSource<bool>? _initTcs;
    private bool _disposed;

    public event EventHandler<string>? NavigationStarting;
    public event EventHandler<string>? SourceChanged;
    public event EventHandler<CoreWebView2NavigationCompletedEventArgs>? NavigationCompleted;

    public string? UserDataFolder { get; set; }

    public IReadOnlyList<string> AllowedHosts { get; set; } = [];

    public bool IsReady => _webView is not null;

    public string? CurrentSource => _webView?.Source;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_webView is not null)
            return;

        _initTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var registration = cancellationToken.Register(() => _initTcs.TrySetCanceled(cancellationToken));
        await _initTcs.Task.ConfigureAwait(true);
    }

    public async Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(true);
        _webView!.Navigate(url);
    }

    public async Task<string?> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(true);
        return await _webView!.ExecuteScriptAsync(script).ConfigureAwait(true);
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _hostHwnd = CreateHostWindow(parent.Handle);
        _ = InitializeAsync();
        return new PlatformHandle(_hostHwnd, "HWND");
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
            var userDataFolder = UserDataFolder
                ?? throw new InvalidOperationException("WebView2 auth profile path is required.");

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder).ConfigureAwait(true);

            var controller = await environment.CreateCoreWebView2ControllerAsync(_hostHwnd).ConfigureAwait(true);
            _controller = controller;
            _webView = controller.CoreWebView2;

            _webView.Settings.AreDefaultScriptDialogsEnabled = true;
            _webView.Settings.IsStatusBarEnabled = false;
            _webView.Settings.AreDevToolsEnabled = false;

            _webView.NavigationStarting += (_, e) =>
            {
                if (!TryGetHost(e.Uri, out var host) || !AuthHostPolicy.IsHostAllowed(host, AllowedHosts))
                {
                    e.Cancel = true;
                    return;
                }

                NavigationStarting?.Invoke(this, e.Uri);
            };
            _webView.SourceChanged += (_, _) =>
                SourceChanged?.Invoke(this, _webView.Source);
            _webView.NavigationCompleted += (_, e) =>
                NavigationCompleted?.Invoke(this, e);

            UpdateBounds();
            _initTcs?.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _initTcs?.TrySetException(ex);
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
        _controller?.Close();
        _controller = null;
        _webView = null;

        if (_hostHwnd != IntPtr.Zero)
        {
            DestroyWindow(_hostHwnd);
            _hostHwnd = IntPtr.Zero;
        }
    }

    private static bool TryGetHost(string url, out string host)
    {
        host = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        host = uri.Host;
        return !string.IsNullOrWhiteSpace(host);
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
