namespace OpenGameHUB.Services.Auth;

// Capture strategies must return only provider callback data (OAuth codes, API keys, etc.),
// never usernames or passwords.
internal interface IAuthCaptureStrategy
{
    string StartUrl { get; }

    string WindowTitleKey { get; }

    string IntroKey { get; }

    IReadOnlyList<string> AllowedHosts { get; }

    object? TryCaptureFromNavigation(string url);

    object? TryCaptureFromResponse(string requestUrl, string responseBody);

    /// <summary>
    /// When true, <see cref="TryCaptureFromNavigationCompleted"/> is used instead of
    /// <see cref="TryCaptureFromNavigation"/> so multi-step logins are not completed mid-flow.
    /// </summary>
    bool UseNavigationCompletedForCapture => false;

    object? TryCaptureFromNavigationCompleted(string url) => null;
}
