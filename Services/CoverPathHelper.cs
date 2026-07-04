using OpenGameHUB.Models;

namespace OpenGameHUB.Services;

internal static class CoverPathHelper
{
    public static string CacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenGameHUB",
        "covers");

    public static string GetCachePath(string gameId) =>
        Path.Combine(CacheDirectory, $"{SanitizeFileName(gameId)}.jpg");

    public static string? ResolveExistingPath(UnifiedGame game) =>
        ResolveExistingPath(game.Id, game.CoverPath);

    public static string? ResolveExistingPath(string gameId, string? coverPath)
    {
        if (!string.IsNullOrWhiteSpace(coverPath) && SafeImageValidator.IsValidImageFile(coverPath))
            return coverPath;

        var cachePath = GetCachePath(gameId);
        return SafeImageValidator.IsValidImageFile(cachePath) ? cachePath : null;
    }

    public static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value;
    }
}
