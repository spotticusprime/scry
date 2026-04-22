using System.Diagnostics;
using Scry.Core;
using Scry.Probes.Configs;
using Scry.Probes.Internal;

namespace Scry.Probes.Executors;

internal sealed class HttpProbeExecutor : IProbeExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;

    public string Kind => "http";

    public HttpProbeExecutor(IHttpClientFactory httpClientFactory)
        => _httpClientFactory = httpClientFactory;

    public async Task<ProbeResult> ExecuteAsync(Probe probe, CancellationToken ct)
    {
        var config = YamlConfig.Deserialize<HttpProbeConfig>(probe.Definition);
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        // Link the probe-level timeout to the caller's token so either can cancel.
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(config.Timeout);

        try
        {
            using var http = _httpClientFactory.CreateClient("scry.probes");
            using var request = new HttpRequestMessage(new HttpMethod(config.Method), config.Url);

            foreach (var (key, value) in config.Headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }

            using var response = await http.SendAsync(request, probeCts.Token);
            var body = await response.Content.ReadAsStringAsync(probeCts.Token);
            sw.Stop();

            var outcome = DetermineOutcome(response, body, config);
            var statusCode = (int)response.StatusCode;

            return new ProbeResult
            {
                WorkspaceId = probe.WorkspaceId,
                ProbeId = probe.Id,
                Outcome = outcome,
                Message = $"{statusCode} {response.ReasonPhrase}",
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
                Attributes = new Dictionary<string, string>
                {
                    ["status_code"] = statusCode.ToString(),
                },
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Probe-level timeout — not a shutdown signal; record as an error result.
            sw.Stop();
            return new ProbeResult
            {
                WorkspaceId = probe.WorkspaceId,
                ProbeId = probe.Id,
                Outcome = ProbeOutcome.Error,
                Message = $"Request to {config.Url} timed out after {config.Timeout.TotalSeconds:0}s",
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }
    }

    private static ProbeOutcome DetermineOutcome(HttpResponseMessage response, string body, HttpProbeConfig config)
    {
        if (config.ExpectedStatus.HasValue)
        {
            return (int)response.StatusCode == config.ExpectedStatus
                ? ProbeOutcome.Ok
                : ProbeOutcome.Crit;
        }

        if (!response.IsSuccessStatusCode)
        {
            return ProbeOutcome.Warn;
        }

        if (config.BodyContains is not null && !body.Contains(config.BodyContains, StringComparison.Ordinal))
        {
            return ProbeOutcome.Crit;
        }

        return ProbeOutcome.Ok;
    }
}
