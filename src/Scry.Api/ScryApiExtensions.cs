using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scry.Api.Endpoints;
using Scry.Data;

namespace Scry.Api;

public static class ScryApiExtensions
{
    /// <summary>
    /// Registers per-request ScryDbContext (scoped) for endpoint injection.
    /// Call after AddDbContextFactory to get both factory (for background services)
    /// and scoped context (for HTTP handlers) from the same pool.
    /// </summary>
    public static IServiceCollection AddScryApi(this IServiceCollection services)
    {
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<ScryDbContext>>().CreateDbContext());
        return services;
    }

    /// <summary>Maps all Scry REST endpoints onto the application.</summary>
    public static IEndpointRouteBuilder MapScryApi(this IEndpointRouteBuilder app)
    {
        // All API endpoints require authentication by default.
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapWorkspaceEndpoints();
        api.MapProbeEndpoints();
        api.MapResultEndpoints();
        api.MapAlertRuleEndpoints();
        api.MapTopologyEndpoints();
        return app;
    }
}
