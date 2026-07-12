using OpenGameHUB.Domain.Enums;

namespace OpenGameHUB.ViewModels;

public sealed class LibraryCollectionItem
{
    public LibraryCollectionItem(LibraryViewKind kind, string label, string? collectionId = null)
    {
        Kind = kind;
        Label = label;
        CollectionId = collectionId;
    }

    public LibraryViewKind Kind { get; }
    public string? CollectionId { get; }
    public string Label { get; }
    public bool IsUserCollection => Kind == LibraryViewKind.UserCollection;
}
