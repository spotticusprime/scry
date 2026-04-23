using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scry.Core;
using Scry.Data;
using Scry.Probes.Alerts;
using Scry.Runner;

namespace Scry.Probes;

internal sealed class ProbeJobHandler : IJobHandler
{
    public const string JobKind = "probe.run";
    public string Kind => JobKind;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IDbContextFactory<ScryDbContext> _factory;
    private readonly IReadOnlyDictionary<string, IProbeExecutor> _executors;
    private readonly AlertEvaluator _alertEvaluator;
    private readonly ILogger<ProbeJobHandler> _logger;

    public ProbeJobHandler(
        IDbContextFactory<ScryDbContext> factory,
        IEnumerable<IProbeExecutor> executors,
        AlertEvaluator alertEvaluator,
        ILogger<ProbeJobHandler> logger)
    {
        _alertEvaluator = alertEvaluator;
        _factory = factory;
        _logger = logger;

        var dict = new Dictionary<string, IProbeExecutor>(StringComparer.OrdinalIgnoreCase);
        foreach (var executor in executors)
        {
            if (!dict.TryAdd(executor.Kind, executor))
            {
                throw new InvalidOperationException(
                    $"Multiple IProbeExecutor registrations for kind '{executor.Kind}'. Each kind must have exactly one executor.");
            }
        }
        _executors = dict;
    }

    public async Task HandleAsync(Job job, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ProbeJobPayload>(job.Payload, JsonOptions)
            ?? throw new InvalidOperationException("Probe job payload deserialized to null.");

        await using var ctx = await _factory.CreateDbContextAsync(ct);

        // Scope the load to the job's workspace — prevents a job from workspace A from
        // executing or leaking results for a probe in workspace B.
        var probe = await ctx.Probes
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.Id == payload.ProbeId && p.WorkspaceId == job.WorkspaceId, ct)
            ?? throw new InvalidOperationException(
                $"Probe {payload.ProbeId} not found in workspace {job.WorkspaceId}.");

        if (!probe.Enabled)
        {
            // "Stop" semantics: the recurring loop ends here. Re-enable the probe and call
            // ScryProbesExtensions.CreateInitialProbeJob to restart the loop.
            _logger.LogDebug("Probe {ProbeId} is disabled — loop stopped.", probe.Id);
            return;
        }

        if (!_executors.TryGetValue(probe.Kind, out var executor))
        {
            throw new InvalidOperationException($"No executor registered for probe kind '{probe.Kind}'.");
        }

        var result = await executor.ExecuteAsync(probe, ct);

        // Add result and next-run job in the same SaveChangesAsync so they succeed or fail together.
        // Using ctx.Jobs.Add directly avoids opening a second context (which IJobQueue.EnqueueAsync does).
        ctx.ProbeResults.Add(result);
        ctx.Jobs.Add(new Job
        {
            WorkspaceId = probe.WorkspaceId,
            Kind = JobKind,
            Payload = JsonSerializer.Serialize(new ProbeJobPayload { ProbeId = probe.Id }, JsonOptions),
            RunAfter = DateTimeOffset.UtcNow + probe.Interval,
            MaxAttempts = 3,
        });

        await ctx.SaveChangesAsync(ct);

        _logger.LogDebug("Probe {ProbeId} ({Kind}) → {Outcome} in {DurationMs}ms",
            probe.Id, probe.Kind, result.Outcome, result.DurationMs);

        // Alert evaluation is best-effort — a failure must not discard the probe result.
        try
        {
            await _alertEvaluator.EvaluateAsync(result, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alert evaluation failed for probe {ProbeId}", probe.Id);
        }
    }
}
