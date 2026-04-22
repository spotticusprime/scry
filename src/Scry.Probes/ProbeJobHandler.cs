using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scry.Core;
using Scry.Data;
using Scry.Runner;

namespace Scry.Probes;

internal sealed class ProbeJobHandler : IJobHandler
{
    public const string JobKind = "probe.run";
    public string Kind => JobKind;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IDbContextFactory<ScryDbContext> _factory;
    private readonly IEnumerable<IProbeExecutor> _executors;
    private readonly IJobQueue _queue;
    private readonly ILogger<ProbeJobHandler> _logger;

    public ProbeJobHandler(
        IDbContextFactory<ScryDbContext> factory,
        IEnumerable<IProbeExecutor> executors,
        IJobQueue queue,
        ILogger<ProbeJobHandler> logger)
    {
        _factory = factory;
        _executors = executors;
        _queue = queue;
        _logger = logger;
    }

    public async Task HandleAsync(Job job, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ProbeJobPayload>(job.Payload, JsonOptions)
            ?? throw new InvalidOperationException("Probe job payload deserialized to null.");

        await using var ctx = await _factory.CreateDbContextAsync(ct);

        var probe = await ctx.Probes
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.Id == payload.ProbeId, ct)
            ?? throw new InvalidOperationException($"Probe {payload.ProbeId} not found.");

        if (!probe.Enabled)
        {
            _logger.LogDebug("Probe {ProbeId} is disabled; skipping execution.", probe.Id);
            return;
        }

        var executor = _executors.FirstOrDefault(e => StringComparer.OrdinalIgnoreCase.Equals(e.Kind, probe.Kind))
            ?? throw new InvalidOperationException($"No executor registered for probe kind '{probe.Kind}'.");

        var result = await executor.ExecuteAsync(probe, ct);

        ctx.ProbeResults.Add(result);

        // Schedule the next run before saving so both writes succeed or fail together.
        var nextJob = new Job
        {
            WorkspaceId = probe.WorkspaceId,
            Kind = JobKind,
            Payload = JsonSerializer.Serialize(new ProbeJobPayload { ProbeId = probe.Id }, JsonOptions),
            RunAfter = DateTimeOffset.UtcNow + probe.Interval,
            MaxAttempts = 3,
        };

        await _queue.EnqueueAsync(nextJob, ct);
        await ctx.SaveChangesAsync(ct);

        _logger.LogDebug("Probe {ProbeId} ({Kind}) → {Outcome} in {DurationMs}ms",
            probe.Id, probe.Kind, result.Outcome, result.DurationMs);
    }
}
