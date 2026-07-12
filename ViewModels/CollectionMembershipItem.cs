namespace OpenGameHUB.ViewModels;

public sealed class CollectionMembershipItem
{
    public CollectionMembershipItem(string collectionId, string name, bool isMember)
    {
        CollectionId = collectionId;
        Name = name;
        IsMember = isMember;
    }

    public string CollectionId { get; }
    public string Name { get; }
    public bool IsMember { get; set; }
}

public sealed record CollectionToggleRequest(GameItemViewModel Game, string CollectionId);
