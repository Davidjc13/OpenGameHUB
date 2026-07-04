namespace OpenGameHUB.Services;

internal sealed class SafeImageDownloader
{
    private readonly HttpClient _httpClient;

    public SafeImageDownloader(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<bool> DownloadAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        if (IsDirectSvgUrl(url))
            return false;

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (!IsAllowedResponseContentType(contentType))
                return false;

            await using var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;

            while ((read = await networkStream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > SafeImageValidator.MaxBytes)
                    return false;

                buffer.Write(chunk, 0, read);
            }

            if (buffer.Length == 0)
                return false;

            var bytes = buffer.ToArray();
            if (!SafeImageValidator.HasValidImageSignature(bytes))
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken);
            return SafeImageValidator.IsValidImageFile(destinationPath);
        }
        catch
        {
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            return false;
        }
    }

    private static bool IsDirectSvgUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

        return uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedResponseContentType(string? contentType)
    {
        if (SafeImageValidator.IsAllowedMimeType(contentType))
            return true;

        return string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }
}
