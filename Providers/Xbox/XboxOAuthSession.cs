namespace OpenGameHUB.Providers.Xbox;

/// <summary>
/// One-shot OAuth request context. Holds the CSRF <c>state</c> and the PKCE
/// <c>code_verifier</c> that must survive from building the authorize URL until
/// the authorization code is exchanged for tokens.
/// </summary>
internal sealed class XboxOAuthSession
{
    public required string AuthorizeUrl { get; init; }

    public required string State { get; init; }

    public required string CodeVerifier { get; init; }
}
