namespace Scry.Probes.Configs;

internal sealed class DnsProbeConfig
{
    public required string Host { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public string? ExpectedAddress { get; init; }
}
