namespace Scry.Probes.Configs;

internal sealed class TlsProbeConfig
{
    public required string Host { get; init; }
    public int Port { get; init; } = 443;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public int WarnDays { get; init; } = 30;
    public int CritDays { get; init; } = 7;
    // Default false: accept self-signed / internal PKI certs for expiry monitoring.
    // Set to true to enforce full chain trust validation in strict PKI environments.
    public bool ValidateRemoteCertificate { get; init; } = false;
}
