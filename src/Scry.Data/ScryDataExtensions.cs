using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scry.Core;

namespace Scry.Data;

public static class ScryDataExtensions
{
    public static IServiceCollection AddScryJobQueue(this IServiceCollection services)
    {
        services.AddSingleton<IJobQueue, MySqlJobQueue>();
        services.AddHostedService(sp =>
            new JobReaper(sp.GetRequiredService<IDbContextFactory<ScryJobDbContext>>()));
        return services;
    }

    public static IServiceCollection AddScryData(this IServiceCollection services)
    {
        services.AddScoped<IAssetHealthService, AssetHealthService>();
        return services;
    }
}
