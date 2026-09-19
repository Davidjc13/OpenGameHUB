using System.Reflection;
using System.Text.Json;
using OpenGameHUB.Infrastructure.Security;

namespace OpenGameHUB.Providers.Epic;

internal sealed record LegendaryManifest(
    string Version,
    string DownloadUrl,
    string Sha256,
    long SizeBytes)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static LegendaryManifest? _cached;

    public static LegendaryManifest Current => _cached ??= LoadEmbedded();

    public void VerifyFile(string path)
    {
        FileIntegrityVerifier.VerifyFileSize(path, SizeBytes);
        FileIntegrityVerifier.VerifySha256(path, Sha256);
    }

    private static LegendaryManifest LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "OpenGameHUB.third-party.legendary.manifest.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");

        var manifest = JsonSerializer.Deserialize<LegendaryManifest>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Could not parse legendary manifest.");

        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl)
            || string.IsNullOrWhiteSpace(manifest.Sha256)
            || manifest.SizeBytes <= 0)
        {
            throw new InvalidOperationException("Legendary manifest is missing required fields.");
        }

        return manifest with { Sha256 = manifest.Sha256.Trim().ToUpperInvariant() };
    }
}
