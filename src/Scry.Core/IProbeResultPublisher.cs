namespace Scry.Core;

public interface IProbeResultPublisher
{
    Task PublishAsync(ProbeResult result, CancellationToken ct = default);
}
