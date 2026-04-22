namespace Scry.Probes.Configs;

internal sealed class TcpProbeConfig
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
