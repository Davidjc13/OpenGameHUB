using System.Text.Json;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Tests;

public sealed class XboxGamePassProductFilterTests
{
    [Fact]
    public void IsInstallableOnPc_accepts_restrictive_pc_installation_terms()
    {
        const string json = """
            {
              "LocalizedProperties": [{ "ProductTitle": "DOOM Eternal (PC)" }],
              "DisplaySkuAvailabilities": [{
                "Sku": {
                  "Properties": {
                    "InstallationTerms": "InstallationTermsRestrictivePC"
                  }
                }
              }]
            }
            """;

        using var document = JsonDocument.Parse(json);
        Assert.True(XboxGamePassProductFilter.IsInstallableOnPc(document.RootElement));
    }

    [Fact]
    public void IsInstallableOnPc_rejects_console_only_installation_terms()
    {
        const string json = """
            {
              "LocalizedProperties": [{ "ProductTitle": "Halo Infinite" }],
              "DisplaySkuAvailabilities": [{
                "Sku": {
                  "Properties": {
                    "InstallationTerms": "InstallationTermsRestrictiveXbox"
                  }
                }
              }]
            }
            """;

        using var document = JsonDocument.Parse(json);
        Assert.False(XboxGamePassProductFilter.IsInstallableOnPc(document.RootElement));
    }

    [Fact]
    public void IsInstallableOnPc_accepts_pc_game_pad_attribute()
    {
        const string json = """
            {
              "LocalizedProperties": [{ "ProductTitle": "Some Game" }],
              "Properties": {
                "Attributes": [{ "Name": "PcGamePad" }]
              },
              "DisplaySkuAvailabilities": []
            }
            """;

        using var document = JsonDocument.Parse(json);
        Assert.True(XboxGamePassProductFilter.IsInstallableOnPc(document.RootElement));
    }

    [Fact]
    public void IsInstallableOnPc_accepts_pc_suffix_in_title()
    {
        const string json = """
            {
              "LocalizedProperties": [{ "ProductTitle": "Forza Horizon 5 (PC)" }],
              "DisplaySkuAvailabilities": []
            }
            """;

        using var document = JsonDocument.Parse(json);
        Assert.True(XboxGamePassProductFilter.IsInstallableOnPc(document.RootElement));
    }
}
