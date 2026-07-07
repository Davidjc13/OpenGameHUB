namespace OpenGameHUB.Services.Auth;

internal interface IAuthCaptureStrategy
{
    string StartUrl { get; }

    string WindowTitleKey { get; }

    string IntroKey { get; }

    IReadOnlyList<string> AllowedHosts { get; }

    object? TryCaptureFromNavigation(string url);

    object? TryCaptureFromResponse(string requestUrl, string responseBody);
}
