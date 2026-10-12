using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Database;

namespace OpenGameHUB.Services.Games;

public sealed class UserCollectionService
{
    private readonly GameDatabase _database;
    private IReadOnlyList<UserCollection> _collections = [];
    private Dictionary<string, HashSet<string>> _membershipIndex = new(StringComparer.Ordinal);
    private Dictionary<string, HashSet<string>> _collectionGameIds = new(StringComparer.Ordinal);

    public UserCollectionService(GameDatabase database) => _database = database;

    public IReadOnlyList<UserCollection> UserCollections => _collections;

    public void Reload()
    {
        _collections = _database.GetUserCollections();
        _membershipIndex = _database.GetMembershipIndex();
        _collectionGameIds = _collections.ToDictionary(
            collection => collection.Id,
            collection => _database.GetCollectionGameIds(collection.Id),
            StringComparer.Ordinal);
    }

    public IReadOnlySet<string> GetCollectionIdsForGame(string gameId) =>
        _membershipIndex.TryGetValue(gameId, out var ids)
            ? ids
            : EmptySet;

    public IReadOnlySet<string> GetGameIdsForCollection(string collectionId) =>
        _collectionGameIds.TryGetValue(collectionId, out var ids)
            ? ids
            : EmptySet;

    public int GetCollectionGameCount(string collectionId) =>
        _collectionGameIds.TryGetValue(collectionId, out var ids) ? ids.Count : 0;

    public UserCollection Create(string name)
    {
        var collection = _database.CreateCollection(name);
        Reload();
        return collection;
    }

    public void Rename(string id, string name)
    {
        _database.RenameCollection(id, name);
        Reload();
    }

    public void Delete(string id)
    {
        _database.DeleteCollection(id);
        Reload();
    }

    public bool AddGame(string collectionId, string gameId)
    {
        _database.AddGameToCollection(collectionId, gameId);
        Reload();
        return GetCollectionIdsForGame(gameId).Contains(collectionId);
    }

    public bool RemoveGame(string collectionId, string gameId)
    {
        _database.RemoveGameFromCollection(collectionId, gameId);
        Reload();
        return !GetCollectionIdsForGame(gameId).Contains(collectionId);
    }

    public bool IsGameInCollection(string collectionId, string gameId) =>
        GetCollectionIdsForGame(gameId).Contains(collectionId);

    public void MergeImported(IReadOnlyList<PortableCollection> collections)
    {
        Reload();
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var collection in _collections)
            byName.TryAdd(collection.Name.Trim(), collection.Id);

        foreach (var imported in collections
                     .OrderBy(collection => collection.SortOrder)
                     .ThenBy(collection => collection.Name, StringComparer.OrdinalIgnoreCase))
        {
            var name = imported.Name?.Trim() ?? string.Empty;
            if (name.Length > MaxImportedNameLength)
                name = name[..MaxImportedNameLength];

            if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl))
                continue;

            if (!byName.TryGetValue(name, out var id))
            {
                id = _database.CreateCollection(name).Id;
                byName[name] = id;
            }

            foreach (var gameId in (imported.GameIds ?? []).Distinct(StringComparer.Ordinal))
            {
                if (!IsPortableGameId(gameId))
                    continue;

                _database.AddGameToCollection(id, gameId);
            }
        }

        Reload();
    }

    public static bool IsPortableGameId(string? gameId) =>
        !string.IsNullOrWhiteSpace(gameId)
        && gameId.Length <= MaxImportedGameIdLength
        && !gameId.Any(char.IsControl)
        && !CustomGameService.IsCustomGameId(gameId);

    private const int MaxImportedNameLength = 200;
    private const int MaxImportedGameIdLength = 512;

    private static readonly HashSet<string> EmptySet = new(StringComparer.Ordinal);
}
