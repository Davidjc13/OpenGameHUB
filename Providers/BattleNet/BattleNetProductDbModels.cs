using ProtoBuf;

namespace OpenGameHUB.Providers.BattleNet;

[ProtoContract]
internal sealed class BattleNetProductInstall
{
    [ProtoMember(1, Name = @"uid")]
    public string Uid { get; set; } = string.Empty;

    [ProtoMember(2, Name = @"product_code")]
    public string ProductCode { get; set; } = string.Empty;

    [ProtoMember(3, Name = @"settings")]
    public BattleNetUserSettings? Settings { get; set; }

    [ProtoMember(4, Name = @"cached_product_state")]
    public BattleNetCachedProductState? CachedProductState { get; set; }
}

[ProtoContract]
internal sealed class BattleNetUserSettings
{
    [ProtoMember(1, Name = @"install_path")]
    public string InstallPath { get; set; } = string.Empty;
}

[ProtoContract]
internal sealed class BattleNetCachedProductState
{
    [ProtoMember(1, Name = @"base_product_state")]
    public BattleNetBaseProductState? BaseProductState { get; set; }
}

[ProtoContract]
internal sealed class BattleNetBaseProductState
{
    [ProtoMember(1, Name = @"installed")]
    public bool Installed { get; set; }

    [ProtoMember(2, Name = @"playable")]
    public bool Playable { get; set; }
}

[ProtoContract]
internal sealed class BattleNetProductDatabase
{
    [ProtoMember(1, Name = @"productInstall")]
    public List<BattleNetProductInstall> ProductInstalls { get; } = [];
}
