using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scry.Core;
using Scry.Data;

namespace Scry.Probes.Alerts;

internal sealed class AlertEvaluator
{
    // Minimum interval between repeat notifications for a Firing alert.
    private static readonly TimeSpan NotifyCooldown = TimeSpan.FromMinutes(5);

    private readonly IDbContextFactory<ScryDbContext> _factory;
    private readonly IReadOnlyDictionary<string, IAlertNotifier> _notifiers;
    private readonly ILogger<AlertEvaluator> _logger;

    public AlertEvaluator(
        IDbContextFactory<ScryDbContext> factory,
        IEnumerable<IAlertNotifier> notifiers,
        ILogger<AlertEvaluator> logger)
    {
        _factory = factory;
        _logger = logger;
        _notifiers = notifiers.ToDictionary(n => n.Kind, StringComparer.OrdinalIgnoreCase);
    }

    public async Task EvaluateAsync(ProbeResult result, CancellationToken ct)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);

        var rules = await ctx.AlertRules
            .IgnoreQueryFilters()
            .Where(r => r.WorkspaceId == result.WorkspaceId
                     && r.Enabled
                     && (r.ProbeIdFilter == null || r.ProbeIdFilter == result.ProbeId))
            .ToListAsync(ct);

        if (rules.Count == 0)
        {
            return;
        }

        var outcomeStr = result.Outcome.ToString();
        var now = DateTimeOffset.UtcNow;

        // Load all open alert events for these rules in one query to avoid N+1.
        var fingerprints = rules.Select(r => $"{r.Id}:{result.ProbeId}").ToList();
        var openEvents = await ctx.AlertEvents
            .IgnoreQueryFilters()
            .Where(e => e.WorkspaceId == result.WorkspaceId
                     && fingerprints.Contains(e.Fingerprint)
                     && e.State != AlertState.Resolved)
            .ToListAsync(ct);
        var eventsByFingerprint = openEvents.ToDictionary(e => e.Fingerprint);

        foreach (var rule in rules)
        {
            var conditionMet = IsConditionMet(rule.Expression, outcomeStr);
            var fingerprint = $"{rule.Id}:{result.ProbeId}";
            eventsByFingerprint.TryGetValue(fingerprint, out var existing);

            if (conditionMet)
            {
                bool shouldNotify;
                if (existing is null)
                {
                    existing = new AlertEvent
                    {
                        WorkspaceId = result.WorkspaceId,
                        AlertRuleId = rule.Id,
                        Fingerprint = fingerprint,
                        State = AlertState.Firing,
                        Severity = rule.Severity,
                        Summary = result.Message,
                        LastNotifiedAt = now,
                    };
                    ctx.AlertEvents.Add(existing);
                    shouldNotify = true;
                }
                else
                {
                    // Only repeat-notify after cooldown.
                    shouldNotify = existing.LastNotifiedAt is null
                        || (now - existing.LastNotifiedAt.Value) >= NotifyCooldown;
                    if (shouldNotify)
                    {
                        existing.LastNotifiedAt = now;
                    }
                }

                if (shouldNotify)
                {
                    await FireNotifierAsync(rule, existing, result, ct);
                }
            }
            else if (existing?.State == AlertState.Firing)
            {
                existing.State = AlertState.Resolved;
                existing.ResolvedAt = now;
                _logger.LogInformation("Alert {AlertName} resolved for probe {ProbeId}", rule.Name, result.ProbeId);
            }
        }

        await ctx.SaveChangesAsync(ct);
    }

    private static bool IsConditionMet(string expression, string outcome)
    {
        // Phase 1: expression is comma-separated ProbeOutcome names, e.g. "Warn,Crit"
        return expression
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(outcome, StringComparer.OrdinalIgnoreCase);
    }

    private async Task FireNotifierAsync(AlertRule rule, AlertEvent evt, ProbeResult result, CancellationToken ct)
    {
        if (rule.NotifierConfig is null)
        {
            return;
        }

        // Determine notifier kind from first JSON field or default to webhook.
        var kind = "webhook";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rule.NotifierConfig);
            if (doc.RootElement.TryGetProperty("kind", out var kindEl))
            {
                kind = kindEl.GetString() ?? "webhook";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse NotifierConfig for alert {AlertName}; defaulting to webhook", rule.Name);
        }

        if (!_notifiers.TryGetValue(kind, out var notifier))
        {
            _logger.LogWarning("No notifier registered for kind '{Kind}' on alert {AlertName}", kind, rule.Name);
            return;
        }

        try
        {
            await notifier.NotifyAsync(rule, evt, result, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notifier '{Kind}' failed for alert {AlertName}", kind, rule.Name);
        }
    }
}
