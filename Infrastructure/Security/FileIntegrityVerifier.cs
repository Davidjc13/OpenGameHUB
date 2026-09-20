using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OpenGameHUB.Infrastructure.Security;

public static partial class FileIntegrityVerifier
{
    private static readonly Regex Sha256HexPattern = Sha256HexRegex();

    public static string ComputeSha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    public static void VerifySha256(string path, string expectedHex)
    {
        var expected = NormalizeSha256Hex(expectedHex);
        var actual = ComputeSha256Hex(path);

        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrityVerificationException(
                Loc.T("IntegritySha256Mismatch", Path.GetFileName(path)));
        }
    }

    public static void VerifyFileSize(string path, long expectedBytes)
    {
        if (expectedBytes <= 0)
            return;

        var info = new FileInfo(path);
        if (info.Length != expectedBytes)
        {
            throw new IntegrityVerificationException(
                Loc.T("IntegritySizeMismatch", Path.GetFileName(path), info.Length, expectedBytes));
        }
    }

    public static string ParseSha256Sidecar(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new IntegrityVerificationException(Loc.T("IntegrityChecksumEmpty"));

        var line = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(line))
            throw new IntegrityVerificationException(Loc.T("IntegrityChecksumEmpty"));

        var token = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token))
            throw new IntegrityVerificationException(Loc.T("IntegrityChecksumEmpty"));

        return NormalizeSha256Hex(token);
    }

    private static string NormalizeSha256Hex(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed["sha256:".Length..];

        if (!Sha256HexPattern.IsMatch(trimmed))
            throw new IntegrityVerificationException(Loc.T("IntegrityChecksumInvalid"));

        return trimmed.ToUpperInvariant();
    }

    [GeneratedRegex("^[0-9A-Fa-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256HexRegex();
}
