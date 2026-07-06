using ProtoBuf;

namespace OpenGameHUB.Providers.Ubisoft;

[ProtoContract]
internal sealed class UbisoftCacheGame
{
    [ProtoMember(1)]
    public uint UplayId { get; set; }

    [ProtoMember(2)]
    public uint InstallId { get; set; }

    [ProtoMember(3)]
    public string GameInfo { get; set; } = string.Empty;
}

[ProtoContract]
internal sealed class UbisoftCacheGameCollection
{
    [ProtoMember(1)]
    public List<UbisoftCacheGame>? Games { get; set; }
}

internal sealed class UbisoftProductInformation
{
    public sealed class Executable
    {
        public sealed class Path
        {
            public string? relative { get; set; }
        }

        public Path? path { get; set; }
        public string? shortcut_name { get; set; }
    }

    public sealed class StartGameItem
    {
        public List<Executable>? executables { get; set; }
    }

    public sealed class StartGame
    {
        public StartGameItem? online { get; set; }
        public StartGameItem? offline { get; set; }
    }

    public sealed class Localizations
    {
        public Dictionary<string, string>? @default { get; set; }
    }

    public sealed class Addon
    {
        public uint id { get; set; }
    }

    public sealed class ThirdPartyPlatform
    {
        public string? name { get; set; }
    }

    public sealed class Product
    {
        public string? name { get; set; }
        public string? thumb_image { get; set; }
        public ThirdPartyPlatform? third_party_platform { get; set; }
        public List<Addon>? addons { get; set; }
        public bool is_ulc { get; set; }
        public StartGame? start_game { get; set; }
    }

    public Product? root { get; set; }
    public Localizations? localizations { get; set; }
}

internal sealed record UbisoftCatalogEntry(
    uint UplayId,
    string Title,
    string? ThumbImageUrl);
