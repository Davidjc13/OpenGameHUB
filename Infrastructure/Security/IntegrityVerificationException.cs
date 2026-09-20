namespace OpenGameHUB.Infrastructure.Security;

public sealed class IntegrityVerificationException : Exception
{
    public IntegrityVerificationException(string message) : base(message)
    {
    }

    public IntegrityVerificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
