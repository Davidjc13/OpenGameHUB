namespace OpenGameHUB.Domain.Models;

/// <summary>
/// A user collection inside a portable preferences file.
/// Game ids for manually added games are omitted.
/// </summary>
public sealed class PortableCollection
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<string> GameIds { get; set; } = [];
}
