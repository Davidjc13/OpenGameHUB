using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Tests;

public sealed class XboxInstallClientTests
{
    [Fact]
    public void BuildInstallAttempts_includes_store_and_xbox_protocols()
    {
        var attempts = XboxInstallClient.BuildInstallAttempts("9NTL0QDWZ4FS", "Microsoft.Halo_8");

        Assert.True(attempts.Count >= 3);
    }
}
