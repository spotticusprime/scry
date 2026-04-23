namespace Scry.Probes.Configs;

internal sealed class AzureMetricProbeConfig
{
    // Azure resource coordinates
    public required string SubscriptionId { get; init; }
    public required string ResourceGroup { get; init; }
    public required string ResourceName { get; init; }

    // Full Azure resource type, e.g. "Microsoft.Web/sites" or "Microsoft.Compute/virtualMachines"
    public required string ResourceType { get; init; }

    // Metrics to query — each metric can have its own thresholds
    public List<AzureMetricThreshold> Metrics { get; init; } = [];

    // Lookback window for metric aggregation
    public int TimeWindowMinutes { get; init; } = 15;

    // Aggregation: Average, Maximum, Minimum, Total, Count
    public string Aggregation { get; init; } = "Average";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

internal sealed class AzureMetricThreshold
{
    public required string Name { get; init; }
    public double? WarnThreshold { get; init; }
    public double? CritThreshold { get; init; }
    // Human-readable unit label stored in attributes, e.g. "%" or "req/min"
    public string Unit { get; init; } = "";
}
