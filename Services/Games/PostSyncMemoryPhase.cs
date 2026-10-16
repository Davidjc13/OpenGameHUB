namespace OpenGameHUB.Services.Games;

[Flags]
public enum PostSyncMemoryPhase
{
    AfterLibraryScan = 1,
    AfterCoverEnrichment = 2,
    Full = AfterLibraryScan | AfterCoverEnrichment
}
