using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Scry.Core;
using Scry.Data;
using Scry.Probes.Alerts;

namespace Scry.Probes.Tests;

public class AlertEvaluatorTests
{
    private static readonly Guid WsId = Guid.NewGuid();

    private static async Task<IDbContextFactory<ScryDbContext>> CreateFactoryAsync()
    {
        var sp = new ServiceCollection()
            .AddDbContextFactory<ScryDbContext>(opt =>
                opt.UseSqlite($"Data Source=file:{Guid.NewGuid()}?mode=memory&cache=shared"))
            .BuildServiceProvider();
        var factory = sp.GetRequiredService<IDbContextFactory<ScryDbContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        await ctx.Database.EnsureCreatedAsync();

        ctx.Workspaces.Add(new Workspace { Id = WsId, Name = "test" });
        await ctx.SaveChangesAsync();
        return factory;
    }

    private static AlertEvaluator MakeEvaluator(
        IDbContextFactory<ScryDbContext> factory,
        IEnumerable<IAlertNotifier>? notifiers = null)
        => new(factory, notifiers ?? [], NullLogger<AlertEvaluator>.Instance);

    private static ProbeResult MakeResult(Guid probeId, ProbeOutcome outcome) => new()
    {
        WorkspaceId = WsId,
        ProbeId = probeId,
        Outcome = outcome,
        StartedAt = DateTimeOffset.UtcNow,
        CompletedAt = DateTimeOffset.UtcNow,
        Message = outcome.ToString(),
    };

    [Fact]
    public async Task No_Rules_Does_Nothing()
    {
        var factory = await CreateFactoryAsync();
        var evaluator = MakeEvaluator(factory);

        // Should not throw when there are no rules.
        await evaluator.EvaluateAsync(MakeResult(Guid.NewGuid(), ProbeOutcome.Crit), CancellationToken.None);
    }

    [Fact]
    public async Task Creates_AlertEvent_When_Condition_Matches()
    {
        var factory = await CreateFactoryAsync();
        var probeId = Guid.NewGuid();

        await using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.AlertRules.Add(new AlertRule
            {
                WorkspaceId = WsId,
                Name = "crit-rule",
                Expression = "Crit",
                Severity = AlertSeverity.Critical,
            });
            await ctx.SaveChangesAsync();
        }

        var evaluator = MakeEvaluator(factory);
        await evaluator.EvaluateAsync(MakeResult(probeId, ProbeOutcome.Crit), CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync();
        var events = await verify.AlertEvents.IgnoreQueryFilters().ToListAsync();
        Assert.Single(events);
        Assert.Equal(AlertState.Firing, events[0].State);
        Assert.Equal(AlertSeverity.Critical, events[0].Severity);
    }

    [Fact]
    public async Task Does_Not_Create_Event_When_Condition_Does_Not_Match()
    {
        var factory = await CreateFactoryAsync();
        var probeId = Guid.NewGuid();

        await using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.AlertRules.Add(new AlertRule
            {
                WorkspaceId = WsId,
                Name = "crit-only",
                Expression = "Crit",
                Severity = AlertSeverity.Critical,
            });
            await ctx.SaveChangesAsync();
        }

        var evaluator = MakeEvaluator(factory);
        await evaluator.EvaluateAsync(MakeResult(probeId, ProbeOutcome.Ok), CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync();
        Assert.Empty(await verify.AlertEvents.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Resolves_Firing_Event_When_Condition_Clears()
    {
        var factory = await CreateFactoryAsync();
        var probeId = Guid.NewGuid();
        Guid ruleId;

        await using (var ctx = await factory.CreateDbContextAsync())
        {
            var rule = new AlertRule
            {
                WorkspaceId = WsId,
                Name = "crit-rule",
                Expression = "Crit",
                Severity = AlertSeverity.Warning,
            };
            ctx.AlertRules.Add(rule);
            ruleId = rule.Id;
            ctx.AlertEvents.Add(new AlertEvent
            {
                WorkspaceId = WsId,
                AlertRuleId = ruleId,
                Fingerprint = $"{ruleId}:{probeId}",
                State = AlertState.Firing,
                Severity = AlertSeverity.Warning,
            });
            await ctx.SaveChangesAsync();
        }

        var evaluator = MakeEvaluator(factory);
        await evaluator.EvaluateAsync(MakeResult(probeId, ProbeOutcome.Ok), CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync();
        var evt = await verify.AlertEvents.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(AlertState.Resolved, evt.State);
        Assert.NotNull(evt.ResolvedAt);
    }

    [Fact]
    public async Task ProbeIdFilter_Scopes_Rule_To_One_Probe()
    {
        var factory = await CreateFactoryAsync();
        var targetProbe = Guid.NewGuid();
        var otherProbe = Guid.NewGuid();

        await using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.AlertRules.Add(new AlertRule
            {
                WorkspaceId = WsId,
                Name = "scoped-rule",
                Expression = "Crit",
                Severity = AlertSeverity.Critical,
                ProbeIdFilter = targetProbe,
            });
            await ctx.SaveChangesAsync();
        }

        var evaluator = MakeEvaluator(factory);
        // Other probe fires Crit — rule should NOT trigger.
        await evaluator.EvaluateAsync(MakeResult(otherProbe, ProbeOutcome.Crit), CancellationToken.None);

        await using var verify = await factory.CreateDbContextAsync();
        Assert.Empty(await verify.AlertEvents.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Calls_Notifier_When_Alert_Fires()
    {
        var factory = await CreateFactoryAsync();
        var notified = new List<(AlertRule, AlertEvent, ProbeResult)>();

        var fakeNotifier = new FakeNotifier(notified);

        await using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.AlertRules.Add(new AlertRule
            {
                WorkspaceId = WsId,
                Name = "notify-rule",
                Expression = "Warn,Crit",
                Severity = AlertSeverity.Warning,
                NotifierConfig = "{\"kind\":\"fake\",\"url\":\"http://x\"}",
            });
            await ctx.SaveChangesAsync();
        }

        var evaluator = MakeEvaluator(factory, [fakeNotifier]);
        await evaluator.EvaluateAsync(MakeResult(Guid.NewGuid(), ProbeOutcome.Warn), CancellationToken.None);

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
}
