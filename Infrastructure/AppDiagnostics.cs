using OpenGameHUB.Domain.Enums;

namespace OpenGameHUB.Infrastructure;

internal static class AppDiagnostics
{
    public static void ReportError(
        string area,
        string operation,
        Exception exception,
        Platform? platform = null,
        string? details = null)
    {
        var platformLabel = platform?.ToString() ?? "n/a";
        var detailSuffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" | details={details}";
        var message =
            $"[{area}] operation={operation} platform={platformLabel} exception={exception.GetType().Name}: {exception.Message}{detailSuffix}{Environment.NewLine}{exception}";
        AppLog.Error(message, platform);
    }

    public static void ReportInfo(
        string area,
        string operation,
        Platform platform,
        string? details = null)
    {
        var detailSuffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" | details={details}";
        AppLog.Information(
            platform,
            $"[{area}] operation={operation} platform={platform}{detailSuffix}");
    }
}
