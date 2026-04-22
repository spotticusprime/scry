using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Scry.Core;
using Scry.Probes.Executors;
using Scry.Runner;

namespace Scry.Probes;

public static class ScryProbesExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Requires IJobQueue and AddScryRunner to already be registered.
    public static IServiceCollection AddScryProbes(this IServiceCollection services)
    {
        // Executors are scoped (per-job) so they can safely inject scoped services in future iterations.
        services.AddScoped<IProbeExecutor, HttpProbeExecutor>();
        services.AddScoped<IProbeExecutor, JsonHttpProbeExecutor>();
        services.AddScoped<IProbeExecutor, TcpProbeExecutor>();
        services.AddScoped<IProbeExecutor, DnsProbeExecutor>();
        services.AddScoped<IProbeExecutor, TlsProbeExecutor>();

        // Handler is scoped so it can inject IDbContextFactory and other scoped deps.
        services.AddJobHandler<ProbeJobHandler>();

        // Named client used by HttpProbeExecutor. Timeout is set per-probe via CancellationToken;
        // the client itself has no global timeout to avoid capping per-probe configurations.
        services.AddHttpClient("scry.probes", client =>
            {
                // No global timeout — each probe manages its own via a linked CancellationTokenSource.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        return services;
    }

    /// <summary>Creates the initial job that seeds a probe's recurring execution loop.</summary>
    public static Job CreateInitialProbeJob(Probe probe) => new()
    {
        WorkspaceId = probe.WorkspaceId,
        Kind = ProbeJobHandler.JobKind,
        Payload = JsonSerializer.Serialize(new ProbeJobPayload { ProbeId = probe.Id }, JsonOptions),
        RunAfter = DateTimeOffset.UtcNow,
        MaxAttempts = 3,
    };
}
