using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Http;

namespace OpenGameHUB.Tests;

public sealed class SafeImageDownloaderTests
{
    [Theory]
    [InlineData("https://cdn.example.com/cover.jpg", true)]
    [InlineData("http://localhost/cover.jpg", true)]
    [InlineData("http://evil.example.com/cover.jpg", false)]
    [InlineData("file:///C:/temp/x.jpg", false)]
    [InlineData("not-a-url", false)]
    public void IsAllowedDownloadUrl_validates_schemes(string url, bool expected)
    {
        Assert.Equal(expected, SafeImageDownloader.IsAllowedDownloadUrl(url));
    }
}
