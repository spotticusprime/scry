namespace Scry.Probes.Configs;

internal sealed class HttpProbeConfig
{
    public required string Url { get; init; }
    public string Method { get; init; } = "GET";
    public Dictionary<string, string> Headers { get; init; } = [];
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    // Null means "any 2xx is OK"
    public int? ExpectedStatus { get; init; }
    public string? BodyContains { get; init; }
}
