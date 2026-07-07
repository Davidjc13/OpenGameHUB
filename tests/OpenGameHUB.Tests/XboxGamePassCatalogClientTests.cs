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

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
