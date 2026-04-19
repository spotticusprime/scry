namespace Scry.Core.Tests;

public class EntityDefaultsTests
{
    [Fact]
    public void Default_enum_values_are_safe_sentinels()
    {
        Assert.Equal(AssetKind.Unknown, default(AssetKind));
        Assert.Equal(RelationshipKind.Unknown, default(RelationshipKind));
        Assert.Equal(ProbeOutcome.Unknown, default(ProbeOutcome));
        Assert.Equal(JobStatus.Pending, default(JobStatus));
        Assert.Equal(AlertState.Pending, default(AlertState));
        Assert.Equal(AlertSeverity.Info, default(AlertSeverity));
    }

    [Fact]
    public void Asset_initializes_with_empty_attributes_and_equal_timestamps()
    {
        var before = DateTimeOffset.UtcNow;
        var asset = new Asset
        {
            WorkspaceId = Guid.NewGuid(),
            Name = "sc.co.gg",
            Kind = AssetKind.Domain,
        };
        var after = DateTimeOffset.UtcNow;

        Assert.NotEqual(Guid.Empty, asset.Id);
        Assert.Empty(asset.Attributes);
        Assert.InRange(asset.CreatedAt, before, after);
        Assert.Equal(asset.CreatedAt, asset.UpdatedAt);
    }

    [Fact]
    public void Workspace_initializes_with_equal_timestamps()
    {
        var workspace = new Workspace { Name = "personal" };

        Assert.Equal(workspace.CreatedAt, workspace.UpdatedAt);
    }

    [Fact]
    public void Probe_initializes_with_equal_timestamps()
    {
        var probe = new Probe
        {
            WorkspaceId = Guid.NewGuid(),
            Name = "cert-expiry-example",
            Kind = "cert-expiry",
            Definition = "host: example.com\nport: 443\n",
        };

        Assert.Equal(probe.CreatedAt, probe.UpdatedAt);
    }

    [Fact]
    public void AlertRule_initializes_with_equal_timestamps()
    {
        var rule = new AlertRule
        {
            WorkspaceId = Guid.NewGuid(),
            Name = "http-5xx-sustained",
            Expression = "rate(http_5xx_total[5m]) > 0.1",
        };

        Assert.Equal(rule.CreatedAt, rule.UpdatedAt);
    }

    [Fact]
    public void Job_requires_workspace_id_and_has_equal_timestamps()
    {
        var workspaceId = Guid.NewGuid();
        var job = new Job { WorkspaceId = workspaceId, Kind = "probe.run", Payload = "{}" };

        Assert.Equal(workspaceId, job.WorkspaceId);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(5, job.MaxAttempts);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.ClaimedBy);
        Assert.Null(job.ClaimedAt);
        Assert.Null(job.LeaseExpiresAt);
        Assert.Equal(job.CreatedAt, job.UpdatedAt);
        Assert.Equal(job.CreatedAt, job.RunAfter);
    }

    [Fact]
    public void Probe_defaults_to_enabled_with_five_minute_interval()
    {
        var probe = new Probe
        {
            WorkspaceId = Guid.NewGuid(),
            Name = "cert-expiry-example",
            Kind = "cert-expiry",
            Definition = "host: example.com\nport: 443\n",
        };

        Assert.True(probe.Enabled);
        Assert.Equal(TimeSpan.FromMinutes(5), probe.Interval);
        Assert.Null(probe.AssetId);
    }

    [Fact]
    public void AlertEvent_requires_severity_and_starts_pending()
    {
        var evt = new AlertEvent
        {
            WorkspaceId = Guid.NewGuid(),
            AlertRuleId = Guid.NewGuid(),
            Fingerprint = "rule=x:asset=y",
            Severity = AlertSeverity.Critical,
        };

        Assert.Equal(AlertState.Pending, evt.State);
        Assert.Equal(AlertSeverity.Critical, evt.Severity);
        Assert.Null(evt.AcknowledgedAt);
        Assert.Null(evt.ResolvedAt);
        Assert.Null(evt.LastNotifiedAt);
        Assert.Empty(evt.Labels);
    }

    [Fact]
    public void ProbeResult_captures_execution_window()
    {
        var started = DateTimeOffset.UtcNow;
        var completed = started.AddMilliseconds(42);
        var result = new ProbeResult
        {
            WorkspaceId = Guid.NewGuid(),
            ProbeId = Guid.NewGuid(),
            Outcome = ProbeOutcome.Ok,
            StartedAt = started,
            CompletedAt = completed,
            DurationMs = 42,
        };

        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
        Assert.Equal(42, result.DurationMs);
        Assert.Equal(started, result.StartedAt);
        Assert.Equal(completed, result.CompletedAt);
    }
}
