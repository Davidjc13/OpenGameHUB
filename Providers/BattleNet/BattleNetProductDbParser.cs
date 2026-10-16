namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetProductDbParser
{
    public static IReadOnlyList<BattleNetProductInstall> Parse(ReadOnlySpan<byte> data)
    {
        var installs = new List<BattleNetProductInstall>();
        foreach (var field in ReadFields(data))
        {
            if (field.Number != 1 || field.Bytes is null)
                continue;

            installs.Add(ParseInstall(field.Bytes));
        }

        return installs;
    }

    private static BattleNetProductInstall ParseInstall(ReadOnlySpan<byte> data)
    {
        var install = new BattleNetProductInstall();
        foreach (var field in ReadFields(data))
        {
            switch (field.Number)
            {
                case 1:
                    install.Uid = field.Text ?? string.Empty;
                    break;
                case 2:
                    install.ProductCode = field.Text ?? string.Empty;
                    break;
                case 3 when field.Bytes is not null:
                    install.Settings = ParseSettings(field.Bytes);
                    break;
                case 4 when field.Bytes is not null:
                    install.CachedProductState = ParseCachedState(field.Bytes);
                    break;
            }
        }

        return install;
    }

    private static BattleNetUserSettings ParseSettings(ReadOnlySpan<byte> data)
    {
        var settings = new BattleNetUserSettings();
        foreach (var field in ReadFields(data))
        {
            if (field.Number == 1)
                settings.InstallPath = field.Text ?? string.Empty;
        }

        return settings;
    }

    private static BattleNetCachedProductState ParseCachedState(ReadOnlySpan<byte> data)
    {
        var cached = new BattleNetCachedProductState();
        foreach (var field in ReadFields(data))
        {
            if (field.Number == 1 && field.Bytes is not null)
                cached.BaseProductState = ParseBaseState(field.Bytes);
        }

        return cached;
    }

    private static BattleNetBaseProductState ParseBaseState(ReadOnlySpan<byte> data)
    {
        var state = new BattleNetBaseProductState();
        foreach (var field in ReadFields(data))
        {
            if (field.Number == 1)
                state.Installed = field.Varint != 0;
            else if (field.Number == 2)
                state.Playable = field.Varint != 0;
        }

        return state;
    }

    private readonly record struct ProtoField(int Number, ulong Varint, string? Text, byte[]? Bytes);

    private static List<ProtoField> ReadFields(ReadOnlySpan<byte> data)
    {
        var fields = new List<ProtoField>();
        var offset = 0;
        while (offset < data.Length)
        {
            if (!TryReadVarint(data, ref offset, out var tag))
                break;

            var number = (int)(tag >> 3);
            var wire = (int)(tag & 0x7);
            switch (wire)
            {
                case 0:
                    if (!TryReadVarint(data, ref offset, out var varint))
                        return fields;
                    fields.Add(new ProtoField(number, varint, null, null));
                    break;
                case 1:
                    if (offset + 8 > data.Length)
                        return fields;
                    offset += 8;
                    break;
                case 2:
                    if (!TryReadVarint(data, ref offset, out var length) || offset + (int)length > data.Length)
                        return fields;
                    var slice = data.Slice(offset, (int)length).ToArray();
                    offset += (int)length;
                    fields.Add(new ProtoField(number, 0, DecodeString(slice), slice));
                    break;
                case 5:
                    if (offset + 4 > data.Length)
                        return fields;
                    offset += 4;
                    break;
                default:
                    return fields;
            }
        }

        return fields;
    }

    private static bool TryReadVarint(ReadOnlySpan<byte> data, ref int offset, out ulong value)
    {
        value = 0;
        var shift = 0;
        while (offset < data.Length && shift < 64)
        {
            var b = data[offset++];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return true;
            shift += 7;
        }

        return false;
    }

    private static string DecodeString(byte[] bytes)
    {
        try
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
