namespace OpenGameHUB.Services.Auth;

internal interface IAuthCaptureStrategy
{
    string StartUrl { get; }

    string WindowTitleKey { get; }

    string IntroKey { get; }

    string? WaitingStatusKey { get; }

    object? TryCaptureFromNavigation(string url);

    Task<object?> TryCaptureFromDomAsync(
        Func<string, Task<string?>> executeScriptAsync,
        string currentUrl);
}
