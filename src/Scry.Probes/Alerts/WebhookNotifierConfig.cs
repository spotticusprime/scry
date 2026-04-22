namespace Scry.Probes.Alerts;

internal sealed class WebhookNotifierConfig
{
    public required string Url { get; init; }
    public string Method { get; init; } = "POST";
    public Dictionary<string, string> Headers { get; init; } = [];
}
