using System.Diagnostics;
using System.Text.Json;
using Scry.Core;
using Scry.Probes.Configs;
using Scry.Probes.Internal;

namespace Scry.Probes.Executors;

internal sealed class JsonHttpProbeExecutor : IProbeExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;

    public string Kind => "http_json";

    public JsonHttpProbeExecutor(IHttpClientFactory httpClientFactory)
        => _httpClientFactory = httpClientFactory;

    public async Task<ProbeResult> ExecuteAsync(Probe probe, CancellationToken ct)
    {
        var config = YamlConfig.Deserialize<JsonHttpProbeConfig>(probe.Definition);
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

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
            // TODO: cap body reads to avoid buffering large responses; fine for Phase 1.
            var body = await response.Content.ReadAsStringAsync(probeCts.Token);
            sw.Stop();

            var statusCode = (int)response.StatusCode;

            // Status code check — same semantics as HttpProbeExecutor.
            if (config.ExpectedStatus.HasValue)
            {
                if (statusCode != config.ExpectedStatus)
                {
                    return StatusResult(probe, started, sw, ProbeOutcome.Crit,
                        $"{statusCode} {response.ReasonPhrase}", statusCode);
                }
            }
            else if (!response.IsSuccessStatusCode)
            {
                return StatusResult(probe, started, sw, ProbeOutcome.Warn,
                    $"{statusCode} {response.ReasonPhrase}", statusCode);
            }

            if (config.JsonPath is null)
            {
                return StatusResult(probe, started, sw, ProbeOutcome.Ok,
                    $"{statusCode} {response.ReasonPhrase}", statusCode);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                return StatusResult(probe, started, sw, ProbeOutcome.Crit,
                    $"Response body is not valid JSON", statusCode);
            }

            using (doc)
            {
                if (!TryGetJsonValue(doc, config.JsonPath, out var actual))
                {
                    return StatusResult(probe, started, sw, ProbeOutcome.Crit,
                        $"JSON path '{config.JsonPath}' not found in response", statusCode);
                }

                if (config.ExpectedValue is not null &&
                    !string.Equals(actual, config.ExpectedValue, StringComparison.Ordinal))
                {
                    return StatusResult(probe, started, sw, ProbeOutcome.Crit,
                        $"JSON path '{config.JsonPath}': expected '{config.ExpectedValue}', got '{actual}'", statusCode);
                }
            }

            return StatusResult(probe, started, sw, ProbeOutcome.Ok,
                $"{statusCode} {response.ReasonPhrase}", statusCode);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
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

    private ProbeResult StatusResult(Probe probe, DateTimeOffset started, Stopwatch sw,
        ProbeOutcome outcome, string message, int statusCode) => new()
        {
            WorkspaceId = probe.WorkspaceId,
            ProbeId = probe.Id,
            Outcome = outcome,
            Message = message,
            DurationMs = sw.ElapsedMilliseconds,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow,
            Attributes = new Dictionary<string, string> { ["status_code"] = statusCode.ToString() },
        };

    // Traverses a dot-notation path (e.g. "data.health.status") through a JSON object.
    private static bool TryGetJsonValue(JsonDocument doc, string dotPath, out string? value)
    {
        var current = doc.RootElement;
        foreach (var part in dotPath.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
            {
                value = null;
                return false;
            }
        }
        // JsonElement.ToString() returns "True"/"False" for booleans — use raw JSON text instead.
        value = current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => current.GetRawText(),
        };
        return true;
    }
}
