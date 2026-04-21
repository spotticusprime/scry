using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scry.Core;

namespace Scry.Data;

public static class ScryDataExtensions
{
    // Requires IDbContextFactory<ScryDbContext> to already be registered (e.g. via AddDbContextFactory).
    public static IServiceCollection AddScryJobQueue(this IServiceCollection services)
    {
        services.AddSingleton<IJobQueue, SqlitePollingJobQueue>();
        services.AddHostedService(sp =>
            new JobReaper(sp.GetRequiredService<IDbContextFactory<ScryDbContext>>()));
        return services;
    }
}
