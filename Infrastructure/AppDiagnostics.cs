using System.Diagnostics;
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
        Trace.TraceError(
            "[{0}] operation={1} platform={2} exception={3}: {4}{5}",
            area,
            operation,
            platformLabel,
            exception.GetType().Name,
            exception.Message,
            detailSuffix);
    }
}
