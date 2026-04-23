using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Scry.Core;
using Scry.Data;
using Scry.Probes.Alerts;

namespace Scry.Probes.Tests;

public class AlertEvaluatorTests : IDisposable
{
    // Hold the connection open so the :memory: database persists across context instances.
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<ScryDbContext> _factory;
    private readonly Guid _wsId = Guid.NewGuid();

    public AlertEvaluatorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ScryDbContext>()
            .UseSqlite(_connection)
            .Options;
        _factory = new FixtureDbContextFactory(options);

        using var ctx = new ScryDbContext(options);
        ctx.Database.EnsureCreated();
        ctx.Workspaces.Add(new Workspace { Id = _wsId, Name = "test" });
        ctx.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private AlertEvaluator MakeEvaluator(IEnumerable<IAlertNotifier>? notifiers = null)
        => new(_factory, notifiers ?? [], NullLogger<AlertEvaluator>.Instance);

    private ProbeResult MakeResult(Guid probeId, ProbeOutcome outcome) => new()
    {
        WorkspaceId = _wsId,
        ProbeId = probeId,
        Outcome = outcome,
        StartedAt = DateTimeOffset.UtcNow,
        CompletedAt = DateTimeOffset.UtcNow,
        Message = outcome.ToString(),
    };

    [Fact]
    public async Task No_Rules_Does_Nothing()
    {
        var evaluator = MakeEvaluator();
        await evaluator.EvaluateAsync(MakeResult(Guid.NewGuid(), ProbeOutcome.Crit), CancellationToken.None);
    }

    [Fact]
    public async Task Creates_AlertEvent_When_Condition_Matches()
    {
        var probeId = Guid.NewGuid();
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            ctx.AlertRules.Add(new AlertRule
            {
                WorkspaceId = _wsId,
                Name = "crit-rule",
                Expression = "Crit",
                Severity = AlertSeverity.Critical,
            });
            await ctx.SaveChangesAsync();
        }

        await MakeEvaluator().EvaluateAsync(MakeResult(probeId, ProbeOutcome.Crit), CancellationToken.None);

        await using var verify = await _factory.CreateDbContextAsync();
        var events = await verify.AlertEvents.IgnoreQueryFilters().ToListAsync();
        Assert.Single(events);
        Assert.Equal(AlertState.Firing, events[0].State);
        Assert.Equal(AlertSeverity.Critical, events[0].Severity);
    }

    [Fact]
    public async Task Does_Not_Create_Event_When_Condition_Does_Not_Match()
    {
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            ctx.AlertRules.Add(new AlertRule
            {
                WorkspaceId = _wsId,
                Name = "crit-only",
                Expression = "Crit",
                Severity = AlertSeverity.Critical,
            });
            await ctx.SaveChangesAsync();
        }

        await MakeEvaluator().EvaluateAsync(MakeResult(Guid.NewGuid(), ProbeOutcome.Ok), CancellationToken.None);

        await using var verify = await _factory.CreateDbContextAsync();
        Assert.Empty(await verify.AlertEvents.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Resolves_Firing_Event_When_Condition_Clears()
    {
        var probeId = Guid.NewGuid();
        Guid ruleId;
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            var rule = new AlertRule
            {
                WorkspaceId = _wsId,
                Name = "crit-rule",
                Expression = "Crit",
                Severity = AlertSeverity.Warning,
            };
            ctx.AlertRules.Add(rule);
            ruleId = rule.Id;
            ctx.AlertEvents.Add(new AlertEvent
            {
                WorkspaceId = _wsId,
                AlertRuleId = ruleId,
                Fingerprint = $"{ruleId}:{probeId}",
                State = AlertState.Firing,
                Severity = AlertSeverity.Warning,
            });
            await ctx.SaveChangesAsync();
        }

        await MakeEvaluator().EvaluateAsync(MakeResult(probeId, ProbeOutcome.Ok), CancellationToken.None);

        await using var verify = await _factory.CreateDbContextAsync();
        var evt = await verify.AlertEvents.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(AlertState.Resolved, evt.State);
        Assert.NotNull(evt.ResolvedAt);
    }

    [Fact]
    public async Task ProbeIdFilter_Scopes_Rule_To_One_Probe()
    {
        var targetProbe = Guid.NewGuid();
        var otherProbe = Guid.NewGuid();
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            ctx.AlertRules.Add(new AlertRule
            {
                WorkspaceId = _wsId,
                Name = "scoped-rule",
                Expression = "Crit",
                Severity = AlertSeverity.Critical,
                ProbeIdFilter = targetProbe,
            });
            await ctx.SaveChangesAsync();
        }

        await MakeEvaluator().EvaluateAsync(MakeResult(otherProbe, ProbeOutcome.Crit), CancellationToken.None);

        await using var verify = await _factory.CreateDbContextAsync();
        Assert.Empty(await verify.AlertEvents.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Calls_Notifier_When_Alert_Fires()
    {
        var notified = new List<(AlertRule, AlertEvent, ProbeResult)>();
        await using (var ctx = await _factory.CreateDbContextAsync())
        {
            ctx.AlertRules.Add(new AlertRule
            {
                WorkspaceId = _wsId,
                Name = "notify-rule",
                Expression = "Warn,Crit",
                Severity = AlertSeverity.Warning,
                NotifierConfig = "{\"kind\":\"fake\",\"url\":\"http://x\"}",
            });
            await ctx.SaveChangesAsync();
        }

        await MakeEvaluator([new FakeNotifier(notified)])
            .EvaluateAsync(MakeResult(Guid.NewGuid(), ProbeOutcome.Warn), CancellationToken.None);

        Assert.Single(notified);
    }

    private sealed class FakeNotifier(List<(AlertRule, AlertEvent, ProbeResult)> log) : IAlertNotifier
    {
        public string Kind => "fake";

        public Task NotifyAsync(AlertRule rule, AlertEvent evt, ProbeResult result, CancellationToken ct)
        {
            log.Add((rule, evt, result));
            return Task.CompletedTask;
        }
    }

    // Thin factory wrapper that reuses a single shared connection.
    private sealed class FixtureDbContextFactory(DbContextOptions<ScryDbContext> options)
        : IDbContextFactory<ScryDbContext>
    {
        public ScryDbContext CreateDbContext() => new(options);
        public Task<ScryDbContext> CreateDbContextAsync(CancellationToken ct = default)
            => Task.FromResult(new ScryDbContext(options));
    }
}
