using System.Diagnostics;
using System.Net.Sockets;
using Scry.Core;
using Scry.Probes.Configs;
using Scry.Probes.Internal;

namespace Scry.Probes.Executors;

internal sealed class TcpProbeExecutor : IProbeExecutor
{
    public string Kind => "tcp";

    public async Task<ProbeResult> ExecuteAsync(Probe probe, CancellationToken ct)
    {
        var config = YamlConfig.Deserialize<TcpProbeConfig>(probe.Definition);
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(config.Timeout);

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(config.Host, config.Port, probeCts.Token);
            sw.Stop();

            return new ProbeResult
            {
                WorkspaceId = probe.WorkspaceId,
                ProbeId = probe.Id,
                Outcome = ProbeOutcome.Ok,
                Message = $"Connected to {config.Host}:{config.Port}",
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
                Attributes = new Dictionary<string, string>
                {
                    ["host"] = config.Host,
                    ["port"] = config.Port.ToString(),
                },
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return new ProbeResult
            {
                WorkspaceId = probe.WorkspaceId,
                ProbeId = probe.Id,
                Outcome = ProbeOutcome.Error,
                Message = $"Connection to {config.Host}:{config.Port} timed out after {config.Timeout.TotalSeconds:0}s",
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }
        catch (SocketException ex)
        {
            sw.Stop();
            return new ProbeResult
            {
                WorkspaceId = probe.WorkspaceId,
                ProbeId = probe.Id,
                Outcome = ProbeOutcome.Crit,
                Message = $"Connection to {config.Host}:{config.Port} failed ({ex.SocketErrorCode}): {ex.Message}",
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
