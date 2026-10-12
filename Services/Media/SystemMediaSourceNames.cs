namespace OpenGameHUB.Services.Media;

public static class SystemMediaSourceNames
{
    public static string Format(string? sourceAppId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppId))
            return string.Empty;

        var bang = sourceAppId.IndexOf('!');
        if (bang > 0)
        {
            var package = sourceAppId[..bang];
            var split = package.LastIndexOf('_');
            return split > 0 ? package[..split] : package;
        }

        var name = sourceAppId;
        var slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0 && slash < name.Length - 1)
            name = name[(slash + 1)..];

        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        return name;
    }
}
