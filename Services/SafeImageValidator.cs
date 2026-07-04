namespace OpenGameHUB.Services;

internal static class SafeImageValidator
{
    private const int MaxImageBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/gif",
        "image/pjpeg",
        "image/x-png"
    };

    public static int MaxBytes => MaxImageBytes;

    public static bool IsAllowedMimeType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var mime = contentType.Split(';', 2)[0].Trim();
        return AllowedMimeTypes.Contains(mime);
    }

    public static bool HasValidImageSignature(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
            return false;

        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return true;

        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return true;

        if (data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F')
            return true;

        return data[0] == (byte)'R'
               && data[1] == (byte)'I'
               && data[2] == (byte)'F'
               && data[3] == (byte)'F'
               && data[8] == (byte)'W'
               && data[9] == (byte)'E'
               && data[10] == (byte)'B'
               && data[11] == (byte)'P';
    }

    public static bool IsValidImageFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is 0 or > MaxImageBytes)
                return false;

            Span<byte> header = stackalloc byte[16];
            using var stream = File.OpenRead(path);
            var read = stream.Read(header);
            return read >= 12 && HasValidImageSignature(header[..read]);
        }
        catch
        {
            return false;
        }
    }
}
