namespace OpenGameHUB.Infrastructure;

internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenGameHUB");

    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "logs");

    public static string LogFilePath { get; } = Path.Combine(LogDirectory, "app.log");
}
