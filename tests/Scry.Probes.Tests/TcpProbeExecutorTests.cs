using Scry.Core;
using Scry.Probes.Executors;

namespace Scry.Probes.Tests;

public class TcpProbeExecutorTests
{
    private static readonly Guid WsId = Guid.NewGuid();

    private static Probe MakeProbe(string yaml) => new()
    {
        WorkspaceId = WsId,
        Name = "test",
        Kind = "tcp",
        Definition = yaml,
    };

    [Fact]
    public async Task Returns_Ok_When_Port_Is_Open()
    {
        // Loopback port 80 may not be open, but any actually listening port works.
        // We bind a random port ourselves to guarantee it's available.
        using var listener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        var executor = new TcpProbeExecutor();
        var probe = MakeProbe($"host: 127.0.0.1\nport: {port}");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        listener.Stop();
        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
        Assert.Contains(port.ToString(), result.Message);
    }

    [Fact]
    public async Task Returns_Crit_When_Port_Is_Closed()
    {
        // Port 1 is reserved and almost certainly not open.
        var executor = new TcpProbeExecutor();
        var probe = MakeProbe("host: 127.0.0.1\nport: 1");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Crit, result.Outcome);
        Assert.Contains("refused", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Returns_Error_On_Timeout()
    {
        // 192.0.2.0/24 is TEST-NET — routable but not assigned; connection will time out.
        var executor = new TcpProbeExecutor();
        var probe = MakeProbe("host: 192.0.2.1\nport: 9999\ntimeout: 00:00:00.100");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Error, result.Outcome);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Result_Has_Correct_Ids_And_Attributes()
    {
        using var listener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        var executor = new TcpProbeExecutor();
        var probe = MakeProbe($"host: 127.0.0.1\nport: {port}");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);
        listener.Stop();

        Assert.Equal(WsId, result.WorkspaceId);
        Assert.Equal(probe.Id, result.ProbeId);
        Assert.Equal("127.0.0.1", result.Attributes["host"]);
        Assert.Equal(port.ToString(), result.Attributes["port"]);
    }
}
