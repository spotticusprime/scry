using System.Text.Json.Serialization;

namespace Scry.Probes;

internal sealed record ProbeJobPayload
{
    [JsonPropertyName("probeId")]
    public Guid ProbeId { get; init; }
}
