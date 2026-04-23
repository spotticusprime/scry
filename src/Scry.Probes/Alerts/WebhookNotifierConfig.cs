using System.Text.Json.Serialization;

namespace Scry.Probes.Alerts;

internal sealed class WebhookNotifierConfig
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; } = "POST";

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; init; } = [];
}
