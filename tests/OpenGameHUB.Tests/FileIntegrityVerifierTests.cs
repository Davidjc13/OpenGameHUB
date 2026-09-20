using OpenGameHUB.Infrastructure.Security;

namespace OpenGameHUB.Tests;

public sealed class FileIntegrityVerifierTests
{
    [Theory]
    [InlineData("A1B2C3D4E5F6789012345678901234567890ABCDEF1234567890ABCDEF123456")]
    [InlineData("a1b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef123456")]
    [InlineData("sha256:A1B2C3D4E5F6789012345678901234567890ABCDEF1234567890ABCDEF123456")]
    [InlineData("A1B2C3D4E5F6789012345678901234567890ABCDEF1234567890ABCDEF123456  OpenGameHUB-Setup.exe")]
    public void ParseSha256Sidecar_accepts_supported_formats(string content)
    {
        var parsed = FileIntegrityVerifier.ParseSha256Sidecar(content);

        Assert.Equal("A1B2C3D4E5F6789012345678901234567890ABCDEF1234567890ABCDEF123456", parsed);
    }

    [Fact]
    public void ParseSha256Sidecar_rejects_invalid_value()
    {
        Assert.Throws<IntegrityVerificationException>(() =>
            FileIntegrityVerifier.ParseSha256Sidecar("not-a-hash"));
    }

    [Fact]
    public void VerifySha256_matches_expected_hash()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ogh-hash-{Guid.NewGuid():N}.bin");
        File.WriteAllText(path, "integrity-test");

        try
        {
            var expected = FileIntegrityVerifier.ComputeSha256Hex(path);
            FileIntegrityVerifier.VerifySha256(path, expected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void VerifySha256_throws_on_mismatch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ogh-hash-{Guid.NewGuid():N}.bin");
        File.WriteAllText(path, "integrity-test");

        try
        {
            Assert.Throws<IntegrityVerificationException>(() =>
                FileIntegrityVerifier.VerifySha256(
                    path,
                    "A1B2C3D4E5F6789012345678901234567890ABCDEF1234567890ABCDEF123456"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void VerifyFileSize_throws_on_mismatch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ogh-size-{Guid.NewGuid():N}.bin");
        File.WriteAllText(path, "12345");

        try
        {
            Assert.Throws<IntegrityVerificationException>(() =>
                FileIntegrityVerifier.VerifyFileSize(path, 999));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
