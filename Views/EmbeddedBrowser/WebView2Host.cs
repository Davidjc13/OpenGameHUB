using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Infrastructure.Browser;

namespace OpenGameHUB.Views.EmbeddedBrowser;

internal readonly record struct WebViewJsonFetchResult(int StatusCode, string? Body, string? Error)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300 && !string.IsNullOrWhiteSpace(Body);
}

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
    public event EventHandler<string>? NavigationCompleted;
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

    public Task NavigateAndWaitAsync(string url, CancellationToken cancellationToken = default) =>
        NavigateAndWaitAsync(url, requireSuccess: true, cancellationToken);

    public async Task NavigateAndWaitAsync(
        string url,
        bool requireSuccess,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(true);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _webView!.NavigationCompleted -= OnCompleted;
            if (e.IsSuccess || !requireSuccess)
                tcs.TrySetResult();
            else
                tcs.TrySetException(new InvalidOperationException($"Navigation failed for {url}."));
        }

        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        _webView!.NavigationCompleted += OnCompleted;
        _webView.Navigate(url);
        await tcs.Task.ConfigureAwait(true);
    }

    /// <summary>
    /// Calls a Battle.net (or same-site) JSON API using the WebView2 cookie jar.
    /// Direct navigation to API URLs often reports NavigationCompleted as failed even when JSON is returned.
    /// </summary>
    public async Task<string?> FetchJsonWithSessionAsync(string url, CancellationToken cancellationToken = default)
    {
        var result = await FetchJsonResultWithSessionAsync(url, cancellationToken).ConfigureAwait(true);
        return result.IsSuccess ? result.Body : null;
    }

    internal async Task<WebViewJsonFetchResult> FetchJsonResultWithSessionAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(true);

        var xsrf = await TryReadXsrfTokenAsync().ConfigureAwait(true);
        var urlLiteral = JsonSerializer.Serialize(url);
        var tokenLiteral = JsonSerializer.Serialize(xsrf ?? string.Empty);
        var script = $$"""
            (async () => {
              try {
                const headers = { 'Accept': 'application/json' };
                const token = {{tokenLiteral}};
                if (token) {
                  headers['X-XSRF-TOKEN'] = token;
                  headers['x-xsrf-token'] = token;
                }
                const response = await fetch({{urlLiteral}}, {
                  credentials: 'include',
                  headers
                });
                const body = await response.text();
                return JSON.stringify({ status: response.status, body });
              } catch (error) {
                return JSON.stringify({ status: 0, body: '', error: String(error) });
              }
            })()
            """;

        var raw = await _webView!.ExecuteScriptAsync(script).ConfigureAwait(true);
        var payload = UnwrapExecuteScriptResult(raw);
        if (string.IsNullOrWhiteSpace(payload))
            return new WebViewJsonFetchResult(0, null, "empty script result");

        try
        {
            using var document = JsonDocument.Parse(payload);
            var status = document.RootElement.TryGetProperty("status", out var statusElement)
                && statusElement.TryGetInt32(out var code)
                    ? code
                    : 0;
            var body = document.RootElement.TryGetProperty("body", out var bodyElement)
                ? bodyElement.GetString()
                : payload;
            var error = document.RootElement.TryGetProperty("error", out var errorElement)
                ? errorElement.GetString()
                : null;
            return new WebViewJsonFetchResult(status, body, error);
        }
        catch (JsonException)
        {
            return new WebViewJsonFetchResult(200, payload, null);
        }
    }

    private async Task<string?> TryReadXsrfTokenAsync()
    {
        if (_webView is null)
            return null;

        try
        {
            var cookies = await _webView.CookieManager.GetCookiesAsync("https://account.battle.net/")
                .ConfigureAwait(true);
            var token = cookies.FirstOrDefault(cookie =>
                cookie.Name.Contains("XSRF", StringComparison.OrdinalIgnoreCase)
                || cookie.Name.Contains("CSRF", StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(token?.Value) ? null : token.Value;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetDocumentTextAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(true);
        var raw = await _webView!.ExecuteScriptAsync(
            "document.body ? document.body.innerText : ''").ConfigureAwait(true);

        return UnwrapExecuteScriptResult(raw);
    }

    /// <summary>
    /// WebView2 returns JSON-encoded script results; large API bodies may surface as a string or as a parsed object.
    /// </summary>
    private static string? UnwrapExecuteScriptResult(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            using var document = JsonDocument.Parse(raw);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.String => document.RootElement.GetString(),
                JsonValueKind.Object or JsonValueKind.Array => document.RootElement.GetRawText(),
                _ => null
            };
        }
        catch (JsonException)
        {
            return raw;
        }
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

            _webView.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess || _webView?.Source is not { } source)
                    return;

                if (!AuthUrl.TryParse(source, out var uri) || !AuthHostPolicy.IsHostAllowed(uri.Host, AllowedHosts))
                    return;

                NavigationCompleted?.Invoke(this, source);
            };

            _webView.WebResourceResponseReceived += (_, e) =>
            {
                if (!AuthUrl.TryParse(e.Request.Uri, out var uri) || !AuthHostPolicy.IsHostAllowed(uri.Host, AllowedHosts))
                    return;

                AuthResponseReceived?.Invoke(this, e);
            };

            _webView.NewWindowRequested += (_, e) =>
            {
                // MFA / SSO sometimes opens a popup; keep everything in this auth surface.
                e.Handled = true;
                if (!string.IsNullOrWhiteSpace(e.Uri))
                    _webView?.Navigate(e.Uri);
            };
            _webView.DownloadStarting += (_, e) => e.Cancel = true;

            UpdateBounds();
            _initTcs.TrySetResult(true);
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(WebView2Host),
                operation: "InitializeAsync",
                exception: ex,
                details: _userDataFolder);
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
