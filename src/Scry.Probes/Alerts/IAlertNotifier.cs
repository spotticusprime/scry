using Scry.Core;

namespace Scry.Probes.Alerts;

internal interface IAlertNotifier
{
    string Kind { get; }
    Task NotifyAsync(AlertRule rule, AlertEvent evt, ProbeResult result, CancellationToken ct);
}
