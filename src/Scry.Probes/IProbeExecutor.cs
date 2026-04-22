using Scry.Core;

namespace Scry.Probes;

public interface IProbeExecutor
{
    string Kind { get; }
    Task<ProbeResult> ExecuteAsync(Probe probe, CancellationToken ct);
}
