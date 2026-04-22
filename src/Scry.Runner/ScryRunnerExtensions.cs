using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scry.Core;

namespace Scry.Runner;

public static class ScryRunnerExtensions
{
    // Requires IJobQueue to already be registered (e.g. via AddScryJobQueue()).
    public static IServiceCollection AddScryRunner(this IServiceCollection services,
        Action<RunnerOptions>? configure = null)
    {
        var options = new RunnerOptions();
        configure?.Invoke(options);

        services.AddHostedService(sp =>
        {
            var queue = sp.GetRequiredService<IJobQueue>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = sp.GetRequiredService<ILogger<JobDispatcher>>();
            return new JobDispatcher(queue, scopeFactory, options.WorkerId, options.LeaseDuration,
                options.PollInterval, logger);
        });

        return services;
    }

    // Registers the handler as scoped so implementations can inject scoped services
    // such as IDbContextFactory<ScryDbContext>.
    public static IServiceCollection AddJobHandler<THandler>(this IServiceCollection services)
        where THandler : class, IJobHandler
    {
        services.AddScoped<IJobHandler, THandler>();
        return services;
    }
}
