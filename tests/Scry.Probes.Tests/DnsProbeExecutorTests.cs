using Scry.Core;
using Scry.Probes.Executors;

namespace Scry.Probes.Tests;

public class DnsProbeExecutorTests
{
    private static readonly Guid WsId = Guid.NewGuid();

    private static Probe MakeProbe(string yaml) => new()
    {
        WorkspaceId = WsId,
        Name = "test",
        Kind = "dns",
        Definition = yaml,
    };

    [Fact]
    public async Task Returns_Ok_When_Host_Resolves()
    {
        var executor = new DnsProbeExecutor();
        var probe = MakeProbe("host: localhost");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
        Assert.Contains("localhost", result.Message);
        Assert.Equal("localhost", result.Attributes["host"]);
        Assert.NotEmpty(result.Attributes["addresses"]);
    }

    [Fact]
    public async Task Returns_Ok_When_Expected_Address_Is_Found()
    {
        var executor = new DnsProbeExecutor();
        var probe = MakeProbe("host: localhost\nexpected_address: 127.0.0.1");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
    }

    [Fact]
    public async Task Returns_Warn_When_Expected_Address_Not_In_Results()
    {
        var executor = new DnsProbeExecutor();
        // localhost will never resolve to 1.2.3.4
        var probe = MakeProbe("host: localhost\nexpected_address: 1.2.3.4");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Warn, result.Outcome);
        Assert.Contains("1.2.3.4", result.Message);
    }

    [Fact]
    public async Task Returns_Crit_When_Host_Does_Not_Resolve()
    {
        var executor = new DnsProbeExecutor();
        // .invalid TLD is guaranteed non-resolvable per RFC 2606.
        var probe = MakeProbe("host: this.host.does.not.exist.invalid");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Crit, result.Outcome);
        Assert.Contains("this.host.does.not.exist.invalid", result.Message);
    }

    [Fact]
    public async Task Result_Has_Correct_Ids()
    {
        var executor = new DnsProbeExecutor();
        var probe = MakeProbe("host: localhost");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(WsId, result.WorkspaceId);
        Assert.Equal(probe.Id, result.ProbeId);
        Assert.True(result.DurationMs >= 0);
        Assert.True(result.CompletedAt >= result.StartedAt);
    }
}
