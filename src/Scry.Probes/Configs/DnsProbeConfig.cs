namespace Scry.Probes.Configs;

internal sealed class DnsProbeConfig
{
    public required string Host { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    // When set, the result is Warn if the address is absent from the resolved set.
    // Warn (not Crit) because the host still resolves — it may be a legitimate DNS
    // change (failover, CDN shift) worth alerting on but not paging for.
    public string? ExpectedAddress { get; init; }
}
