namespace OpenGameHUB.Providers.Xbox;

internal static class XboxCatalogMerger
{
    public static IReadOnlyList<XboxCatalogEntry> Merge(
        IReadOnlyList<XboxCatalogEntry> catalog,
        IReadOnlyList<XboxCatalogEntry> history,
        bool includeHistoryOnly = true)
    {
        var results = new Dictionary<string, XboxCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in catalog)
            results[Key(entry)] = entry;

        foreach (var entry in history)
        {
            var key = Key(entry);
            if (results.TryGetValue(key, out var existing))
            {
                results[key] = existing with
                {
                    PlaytimeMinutes = entry.PlaytimeMinutes ?? existing.PlaytimeMinutes,
                    LastPlayed = entry.LastPlayed ?? existing.LastPlayed,
                    TitleId = string.IsNullOrWhiteSpace(entry.TitleId) ? existing.TitleId : entry.TitleId,
                    Pfn = string.IsNullOrWhiteSpace(existing.Pfn) ? entry.Pfn : existing.Pfn,
                    StoreProductId = existing.StoreProductId ?? entry.StoreProductId
                };
            }
            else if (includeHistoryOnly)
            {
                results[key] = entry;
            }
        }

        return results.Values.ToList();
    }

    private static string Key(XboxCatalogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Pfn))
            return entry.Pfn;

        if (!string.IsNullOrWhiteSpace(entry.StoreProductId))
            return entry.StoreProductId;

        return MetadataSearchHelper.NormalizeTitle(entry.Title);
    }
}
