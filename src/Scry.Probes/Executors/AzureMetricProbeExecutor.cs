using System.Diagnostics;
using Azure;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Scry.Core;
using Scry.Probes.Azure;
using Scry.Probes.Configs;
using Scry.Probes.Internal;

namespace Scry.Probes.Executors;

internal sealed class AzureMetricProbeExecutor : IProbeExecutor
{
    public string Kind => "azure_metric";

    private readonly AzureCredentialProvider _credentials;

    public AzureMetricProbeExecutor(AzureCredentialProvider credentials)
    {
        _credentials = credentials;
    }

    public async Task<ProbeResult> ExecuteAsync(Probe probe, CancellationToken ct)
    {
        var config = YamlConfig.Deserialize<AzureMetricProbeConfig>(probe.Definition);
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(config.Timeout);

        try
        {
            var client = new MetricsQueryClient(_credentials.Credential);

            var resourceId = $"/subscriptions/{config.SubscriptionId}"
                + $"/resourceGroups/{config.ResourceGroup}"
                + $"/providers/{config.ResourceType}/{config.ResourceName}";

            var metricNames = config.Metrics.Select(m => m.Name).ToList();
            var timeRange = new QueryTimeRange(TimeSpan.FromMinutes(config.TimeWindowMinutes));

            var aggregation = config.Aggregation.ToLowerInvariant() switch
            {
                "maximum" => MetricAggregationType.Maximum,
                "minimum" => MetricAggregationType.Minimum,
                "total"   => MetricAggregationType.Total,
                "count"   => MetricAggregationType.Count,
                _         => MetricAggregationType.Average,
            };

            var response = await client.QueryResourceAsync(
                resourceId,
                metricNames,
                new MetricsQueryOptions
                {
                    TimeRange = timeRange,
                    Granularity = TimeSpan.FromMinutes(Math.Max(1, config.TimeWindowMinutes)),
                },
                probeCts.Token);

            sw.Stop();

            var attributes = new Dictionary<string, string>
            {
                ["resource"] = $"{config.ResourceGroup}/{config.ResourceName}",
                ["resource_type"] = config.ResourceType,
                ["time_window_minutes"] = config.TimeWindowMinutes.ToString(),
            };

            var outcome = ProbeOutcome.Ok;
            var messages = new List<string>();

            foreach (var metric in response.Value.Metrics)
            {
                var threshold = config.Metrics.FirstOrDefault(m =>
                    string.Equals(m.Name, metric.Name, StringComparison.OrdinalIgnoreCase));

                double? latestValue = null;
                foreach (var ts in metric.TimeSeries)
                {
                    var lastPoint = ts.Values.LastOrDefault(v => v != null);
                    if (lastPoint is not null)
                    {
                        latestValue = aggregation switch
                        {
                            MetricAggregationType.Maximum => lastPoint.Maximum,
                            MetricAggregationType.Minimum => lastPoint.Minimum,
                            MetricAggregationType.Total   => lastPoint.Total,
                            MetricAggregationType.Count   => lastPoint.Count,
                            _                             => lastPoint.Average,
                        };
                        break;
                    }
                }

                var valueStr = latestValue.HasValue ? $"{latestValue.Value:F1}{threshold?.Unit}" : "no data";
                attributes[$"metric_{metric.Name}"] = valueStr;

                if (latestValue.HasValue && threshold is not null)
                {
                    if (threshold.CritThreshold.HasValue && latestValue.Value >= threshold.CritThreshold.Value)
                    {
                        outcome = ProbeOutcome.Crit;
                        messages.Add($"{metric.Name}={valueStr} ≥ crit({threshold.CritThreshold.Value})");
                    }
                    else if (threshold.WarnThreshold.HasValue && latestValue.Value >= threshold.WarnThreshold.Value)
                    {
                        if (outcome < ProbeOutcome.Crit)
                        {
                            outcome = ProbeOutcome.Warn;
                        }
                        messages.Add($"{metric.Name}={valueStr} ≥ warn({threshold.WarnThreshold.Value})");
                    }
                }
            }

            var message = messages.Count > 0
                ? string.Join("; ", messages)
                : $"{config.ResourceName}: all metrics within thresholds";

            return new ProbeResult
            {
                WorkspaceId = probe.WorkspaceId,
                ProbeId = probe.Id,
                Outcome = outcome,
                Message = message,
                DurationMs = sw.ElapsedMilliseconds,
                StartedAt = started,
                CompletedAt = DateTimeOffset.UtcNow,
                Attributes = attributes,
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return Fail(probe, started, sw, ProbeOutcome.Error,
                $"Azure metric query for {config.ResourceName} timed out after {config.Timeout.TotalSeconds:0}s");
        }
        catch (RequestFailedException ex)
        {
            sw.Stop();
            var hint = ex.Status == 401 || ex.Status == 403
                ? " — check Scry:Azure credentials and RBAC role (Monitoring Reader)"
                : "";
            return Fail(probe, started, sw, ProbeOutcome.Error,
                $"Azure API error {ex.Status}: {ex.Message}{hint}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Fail(probe, started, sw, ProbeOutcome.Error, ex.Message);
        }
    }

    private static ProbeResult Fail(Probe probe, DateTimeOffset started, Stopwatch sw,
        ProbeOutcome outcome, string message) => new()
    {
        WorkspaceId = probe.WorkspaceId,
        ProbeId = probe.Id,
        Outcome = outcome,
        Message = message,
        DurationMs = sw.ElapsedMilliseconds,
        StartedAt = started,
        CompletedAt = DateTimeOffset.UtcNow,
    };
}
