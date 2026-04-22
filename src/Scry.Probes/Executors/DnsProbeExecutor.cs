using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Scry.Core;
using Scry.Probes.Configs;
using Scry.Probes.Internal;

namespace Scry.Probes.Executors;

internal sealed class DnsProbeExecutor : IProbeExecutor
{
    public string Kind => "dns";

    public async Task<ProbeResult> ExecuteAsync(Probe probe, CancellationToken ct)
    {
        var config = YamlConfig.Deserialize<DnsProbeConfig>(probe.Definition);
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(config.Timeout);

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(config.Host, probeCts.Token);
            sw.Stop();

            var addrList = addresses.Select(a => a.ToString()).ToArray();
            var attrs = new Dictionary<string, string>
            {
                ["host"] = config.Host,
                ["addresses"] = string.Join(",", addrList),
            };

            if (config.ExpectedAddress is not null &&
                !addrList.Contains(config.ExpectedAddress, StringComparer.OrdinalIgnoreCase))
            {
                return new ProbeResult
                {
                    WorkspaceId = probe.WorkspaceId,
                    ProbeId = probe.Id,
                    Outcome = ProbeOutcome.Warn,
                    Message = $"{config.Host} resolved but {config.ExpectedAddress} not in [{string.Join(", ", addrList)}]",
                    DurationMs = sw.ElapsedMilliseconds,
                    StartedAt = started,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Attributes = attrs,
                };
            }

            return new ProbeResult
            {
                WorkspaceId = probe.WorkspaceId,
                ProbeId = probe.Id,
                Outcome = ProbeOutcome.Ok,
                Message = $"{config.Host} → [{string.Join(", ", addrList)}]",
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
                Attributes = attrs,
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
                Message = $"DNS lookup for {config.Host} timed out after {config.Timeout.TotalSeconds:0}s",
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
                Message = $"DNS lookup for {config.Host} failed ({ex.SocketErrorCode}): {ex.Message}",
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
