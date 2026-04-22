using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Scry.Core;
using Scry.Probes.Configs;
using Scry.Probes.Internal;

namespace Scry.Probes.Executors;

internal sealed class TlsProbeExecutor : IProbeExecutor
{
    public string Kind => "tls";

    public async Task<ProbeResult> ExecuteAsync(Probe probe, CancellationToken ct)
    {
        var config = YamlConfig.Deserialize<TlsProbeConfig>(probe.Definition);
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(config.Timeout);

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(config.Host, config.Port, probeCts.Token);

            using var ssl = new SslStream(tcp.GetStream(), false);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = config.Host,
                // Accept any cert — we're reading it for expiry, not validating chain trust.
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            }, probeCts.Token);

            sw.Stop();

            // SslStream.RemoteCertificate is always X509Certificate2 at runtime.
            var cert = ssl.RemoteCertificate as X509Certificate2;
            if (cert is null)
            {
                return new ProbeResult
                {
                    WorkspaceId = probe.WorkspaceId,
                    ProbeId = probe.Id,
                    Outcome = ProbeOutcome.Error,
                    Message = $"No certificate returned by {config.Host}:{config.Port}",
                    DurationMs = sw.ElapsedMilliseconds,
                    StartedAt = started,
                    CompletedAt = DateTimeOffset.UtcNow,
                };
            }

            var expiry = cert.NotAfter.ToUniversalTime();
            var daysLeft = (int)(expiry - DateTime.UtcNow).TotalDays;
            var outcome = daysLeft <= config.CritDays ? ProbeOutcome.Crit
                : daysLeft <= config.WarnDays ? ProbeOutcome.Warn
                : ProbeOutcome.Ok;

            return new ProbeResult
            {
                WorkspaceId = probe.WorkspaceId,
                ProbeId = probe.Id,
                Outcome = outcome,
                Message = $"{config.Host}:{config.Port} cert expires {expiry:yyyy-MM-dd} ({daysLeft} days)",
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
                Attributes = new Dictionary<string, string>
                {
                    ["host"] = config.Host,
                    ["port"] = config.Port.ToString(),
                    ["expires_at"] = expiry.ToString("O"),
                    ["days_left"] = daysLeft.ToString(),
                    ["subject"] = cert.Subject,
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
                Message = $"TLS connection to {config.Host}:{config.Port} timed out after {config.Timeout.TotalSeconds:0}s",
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }
        catch (Exception ex) when (ex is SocketException or AuthenticationException or IOException)
        {
            sw.Stop();
            return new ProbeResult
            {
                WorkspaceId = probe.WorkspaceId,
                ProbeId = probe.Id,
                Outcome = ProbeOutcome.Crit,
                Message = $"TLS connection to {config.Host}:{config.Port} failed: {ex.Message}",
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
