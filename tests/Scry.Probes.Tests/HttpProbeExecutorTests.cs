using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Scry.Core;
using Scry.Probes.Executors;

namespace Scry.Probes.Tests;

public class HttpProbeExecutorTests
{
    private static readonly Guid WsId = Guid.NewGuid();

    private static (HttpProbeExecutor, MockHttpHandler) MakeExecutor(
        HttpStatusCode status, string body = "")
    {
        var handler = new MockHttpHandler(status, body);
        var sp = new ServiceCollection()
            .AddHttpClient("scry.probes")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .Services
            .BuildServiceProvider();
        return (new HttpProbeExecutor(sp.GetRequiredService<IHttpClientFactory>()), handler);
    }

    private static Probe MakeProbe(string yaml) => new()
    {
        WorkspaceId = WsId,
        Name = "test",
        Kind = "http",
        Definition = yaml,
    };

    [Fact]
    public async Task Returns_Ok_For_200_Response()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "healthy");
        var probe = MakeProbe("url: http://test.local/health");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
        Assert.Contains("200", result.Message);
        Assert.Equal("200", result.Attributes["status_code"]);
    }

    [Fact]
    public async Task Returns_Warn_For_Non_Success_Status()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.ServiceUnavailable);
        var probe = MakeProbe("url: http://test.local/health");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Warn, result.Outcome);
        Assert.Equal("503", result.Attributes["status_code"]);
    }

    [Fact]
    public async Task Returns_Crit_When_Expected_Status_Not_Met()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK);
        var probe = MakeProbe("url: http://test.local\nexpected_status: 201");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Crit, result.Outcome);
    }

    [Fact]
    public async Task Returns_Ok_When_Expected_Status_Met()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.Created);
        var probe = MakeProbe("url: http://test.local\nexpected_status: 201");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
    }

    [Fact]
    public async Task Returns_Crit_When_Body_Does_Not_Contain_Expected_String()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "error");
        var probe = MakeProbe("url: http://test.local\nbody_contains: healthy");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Crit, result.Outcome);
    }

    [Fact]
    public async Task Returns_Ok_When_Body_Contains_Expected_String()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "status: healthy");
        var probe = MakeProbe("url: http://test.local\nbody_contains: healthy");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
    }

    [Fact]
    public async Task Returns_Crit_When_Expected_Status_Met_But_Body_Fails()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "error");
        var probe = MakeProbe("url: http://test.local\nexpected_status: 200\nbody_contains: healthy");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Crit, result.Outcome);
    }

    [Fact]
    public async Task Returns_Ok_When_Expected_Status_And_Body_Both_Match()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK, "status: healthy");
        var probe = MakeProbe("url: http://test.local\nexpected_status: 200\nbody_contains: healthy");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Ok, result.Outcome);
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
        var executor = new HttpProbeExecutor(sp.GetRequiredService<IHttpClientFactory>());
        var probe = MakeProbe("url: http://test.local\ntimeout: 00:00:00.050");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Error, result.Outcome);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Result_Has_Correct_Workspace_And_Probe_Ids()
    {
        var (executor, _) = MakeExecutor(HttpStatusCode.OK);
        var probe = MakeProbe("url: http://test.local");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(WsId, result.WorkspaceId);
        Assert.Equal(probe.Id, result.ProbeId);
        Assert.True(result.DurationMs >= 0);
        Assert.True(result.CompletedAt >= result.StartedAt);
    }

    // Test helpers

    internal sealed class MockHttpHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    internal sealed class TimeoutHttpHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
