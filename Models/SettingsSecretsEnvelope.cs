namespace OpenGameHUB.Models;

internal sealed class SettingsSecretsEnvelope
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public SettingsSecrets Secrets { get; set; } = new();
}
