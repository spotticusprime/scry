namespace Scry.Probes.Configs;

internal sealed class JsonHttpProbeConfig
{
    public required string Url { get; init; }
    public string Method { get; init; } = "GET";
    public Dictionary<string, string> Headers { get; init; } = [];
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public int? ExpectedStatus { get; init; }
    // Dot-notation path into the JSON body, e.g. "status" or "data.health"
    public string? JsonPath { get; init; }
    public string? ExpectedValue { get; init; }
}
