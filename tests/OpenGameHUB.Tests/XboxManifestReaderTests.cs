using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Tests;

public sealed class XboxManifestReaderTests
{
    [Fact]
    public void Read_parses_display_name_and_executable()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ogh-xbox-{Guid.NewGuid():N}"));
        var manifest = Path.Combine(dir.FullName, "AppxManifest.xml");
        File.WriteAllText(manifest, """
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
              <Properties>
                <DisplayName>Halo Infinite</DisplayName>
                <Category>games</Category>
              </Properties>
              <Applications>
                <Application Id="App" Executable="Game.exe" />
              </Applications>
            </Package>
            """);

        try
        {
            var info = XboxManifestReader.Read(manifest);
            Assert.Equal("Halo Infinite", info.DisplayName);
            Assert.Equal("Game.exe", info.Executable);
            Assert.True(XboxManifestReader.IsGameManifest(info));
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public void ResolveManifestPath_finds_direct_and_content_paths()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ogh-xbox-{Guid.NewGuid():N}"));
        var direct = Path.Combine(root.FullName, "AppxManifest.xml");
        File.WriteAllText(direct, "<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\" />");

        try
        {
            Assert.Equal(direct, XboxManifestReader.ResolveManifestPath(root.FullName));

            File.Delete(direct);
            var contentDir = Directory.CreateDirectory(Path.Combine(root.FullName, "Content"));
            var nested = Path.Combine(contentDir.FullName, "AppxManifest.xml");
            File.WriteAllText(nested, "<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\" />");
            Assert.Equal(nested, XboxManifestReader.ResolveManifestPath(root.FullName));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }
}
