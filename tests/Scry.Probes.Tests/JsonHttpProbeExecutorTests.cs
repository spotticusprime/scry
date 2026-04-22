using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Scry.Core;
using Scry.Probes.Executors;

namespace Scry.Probes.Tests;

public class JsonHttpProbeExecutorTests
{
    private static readonly Guid WsId = Guid.NewGuid();

    private static (JsonHttpProbeExecutor, MockHttpHandler) MakeExecutor(
        HttpStatusCode status, string body = "")
    {
        var handler = new MockHttpHandler(status, body);
        var sp = new ServiceCollection()
            .AddHttpClient("scry.probes")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .Services
            .BuildServiceProvider();
        return (new JsonHttpProbeExecutor(sp.GetRequiredService<IHttpClientFactory>()), handler);
    }

    private static Probe MakeProbe(string yaml) => new()
    {
        WorkspaceId = WsId,
        Name = "test",
        Kind = "http_json",
        Definition = yaml,
    };

    [Fact]
    public async Task Returns_Ok_For_200_With_No_Json_Path()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "{\"status\":\"ok\"}");
        var probe = MakeProbe("url: http://test.local/health");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
        Assert.Equal("200", result.Attributes["status_code"]);
    }

    [Fact]
    public async Task Returns_Ok_When_Json_Path_Value_Matches()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "{\"status\":\"ok\"}");
        var probe = MakeProbe("url: http://test.local\njson_path: status\nexpected_value: ok");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
    }

    [Fact]
    public async Task Returns_Ok_With_Nested_Json_Path()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "{\"data\":{\"health\":\"up\"}}");
        var probe = MakeProbe("url: http://test.local\njson_path: data.health\nexpected_value: up");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
    }

    [Fact]
    public async Task Returns_Crit_When_Json_Path_Value_Mismatches()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "{\"status\":\"degraded\"}");
        var probe = MakeProbe("url: http://test.local\njson_path: status\nexpected_value: ok");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Crit, result.Outcome);
        Assert.Contains("degraded", result.Message);
    }

    [Fact]
    public async Task Returns_Crit_When_Json_Path_Not_Found()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "{\"other\":\"value\"}");
        var probe = MakeProbe("url: http://test.local\njson_path: status");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Crit, result.Outcome);
        Assert.Contains("status", result.Message);
    }

    [Fact]
    public async Task Returns_Crit_When_Body_Is_Not_Valid_Json()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "not-json");
        var probe = MakeProbe("url: http://test.local\njson_path: status");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Crit, result.Outcome);
        Assert.Contains("JSON", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Returns_Warn_For_Non_Success_Status_Without_Expected_Status()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.ServiceUnavailable, "{}");
        var probe = MakeProbe("url: http://test.local\njson_path: status\nexpected_value: ok");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Warn, result.Outcome);
    }

    [Fact]
    public async Task Returns_Crit_When_Expected_Status_Not_Met()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "{\"status\":\"ok\"}");
        var probe = MakeProbe("url: http://test.local\nexpected_status: 201\njson_path: status\nexpected_value: ok");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Crit, result.Outcome);
    }

    [Fact]
    public async Task Returns_Error_On_Timeout()
    {
        var handler = new TimeoutHttpHandler();
        var sp = new ServiceCollection()
            .AddHttpClient("scry.probes")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .Services
            .BuildServiceProvider();
        var executor = new JsonHttpProbeExecutor(sp.GetRequiredService<IHttpClientFactory>());
        var probe = MakeProbe("url: http://test.local\ntimeout: 00:00:00.050\njson_path: status");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Error, result.Outcome);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Result_Has_Correct_Workspace_And_Probe_Ids()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "{\"status\":\"ok\"}");
        var probe = MakeProbe("url: http://test.local\njson_path: status");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(WsId, result.WorkspaceId);
        Assert.Equal(probe.Id, result.ProbeId);
        Assert.True(result.DurationMs >= 0);
        Assert.True(result.CompletedAt >= result.StartedAt);
    }
}
