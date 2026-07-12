using System.Text.Json;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Tests;

public sealed class XboxGamePassCatalogClientTests
{
    [Fact]
    public void MapProduct_reads_pfn_and_title()
    {
        const string json = """
            {
              "ProductId": "9PK09BL31FK1",
              "LocalizedProperties": [{ "ProductTitle": "DOOM Eternal (PC)" }],
              "Properties": {
                "PackageFamilyName": "BethesdaSoftworks.DOOMEternal_3275kfvn8vcwc"
              }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var entry = XboxGamePassCatalogClient.MapProduct(document.RootElement);

        Assert.NotNull(entry);
        Assert.Equal("9PK09BL31FK1", entry.StoreProductId);
        Assert.Equal("BethesdaSoftworks.DOOMEternal_3275kfvn8vcwc", entry.Pfn);
        Assert.Equal("DOOM Eternal", entry.Title);
    }

    [Fact]
    public async Task GetPcGamePassProductIdsAsync_reads_sigl_ids()
    {
        const string siglJson = """
            [
              { "id": "9PK09BL31FK1" },
              { "id": "9NBLGGH4R2R6" }
            ]
            """;

        using var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(siglJson)
            });

        using var httpClient = new HttpClient(handler);
        var ids = await XboxGamePassCatalogClient.GetPcGamePassProductIdsAsync(
            httpClient,
            "US",
            "en-us");

        Assert.Equal(2, ids.Count);
        Assert.Contains("9PK09BL31FK1", ids);
    }

    [Fact]
    public async Task ResolveProductIdsWithFallbackAsync_retries_us_when_local_market_is_empty()
    {
        using var handler = new FakeHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("market=ES", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("[]")
                };
            }

            if (url.Contains("market=US", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{ "id": "9PK09BL31FK1" }]""")
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var client = new XboxGamePassCatalogClient(httpClient);

        var (market, language, productIds) =
            await client.ResolveProductIdsWithFallbackAsync("ES", "es-es");

        Assert.Equal("US", market);
        Assert.Equal("en-us", language);
        Assert.Single(productIds);
        Assert.Equal("9PK09BL31FK1", productIds[0]);
    }

    [Fact]
    public async Task GetInstallablePcGamesAsync_uses_us_catalog_when_local_sigl_is_empty()
    {
        const string productJson = """
            {
              "Products": [{
                "ProductId": "9PK09BL31FK1",
                "LocalizedProperties": [{ "ProductTitle": "DOOM Eternal (PC)" }],
                "Properties": {
                  "PackageFamilyName": "BethesdaSoftworks.DOOMEternal_3275kfvn8vcwc"
                },
                "DisplaySkuAvailabilities": [{
                  "Sku": {
                    "Properties": {
                      "InstallationTerms": "InstallationTermsRestrictivePC"
                    }
                  }
                }]
              }]
            }
            """;

        using var handler = new FakeHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("catalog.gamepass.com/sigls", StringComparison.OrdinalIgnoreCase))
            {
                if (url.Contains("market=ES", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("[]")
                    };
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("""[{ "id": "9PK09BL31FK1" }]""")
                };
            }

            if (url.Contains("displaycatalog.mp.microsoft.com", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(productJson)
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var client = new XboxGamePassCatalogClient(httpClient);

        var entries = await client.GetInstallablePcGamesAsync("ES", "es-es");

        Assert.Single(entries);
        Assert.Equal("DOOM Eternal", entries[0].Title);
        Assert.Equal("9PK09BL31FK1", entries[0].StoreProductId);
    }
}
