using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Scry.Core;
using Scry.Data;

namespace Scry.Api.Endpoints;

internal static class ResultEndpoints
{
    internal static IEndpointRouteBuilder MapResultEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/workspaces/{workspaceId:guid}/results").WithTags("Results");

        // Latest result per probe.
        group.MapGet("/latest", async (Guid workspaceId, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            // EF Core 10 translates GroupBy/First to a single ROW_NUMBER() OVER(PARTITION BY) window query.
            var results = await ctx.ProbeResults
                .GroupBy(r => r.ProbeId)
                .Select(g => g.OrderByDescending(r => r.CompletedAt).First())
                .ToListAsync();
            return Results.Ok(results.Select(ToDto));
        });

        // Last N results for a specific probe.
        group.MapGet("/{probeId:guid}", async (Guid workspaceId, Guid probeId, ScryDbContext ctx, int limit = 0) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var take = Math.Clamp(limit == 0 ? 50 : limit, 1, 500);
            var results = await ctx.ProbeResults
                .Where(r => r.ProbeId == probeId)
                .OrderByDescending(r => r.CompletedAt)
                .Take(take)
                .ToListAsync();
            return Results.Ok(results.Select(ToDto));
        });

        // Timeseries: bucketed probe results for charting.
        group.MapGet("/{probeId:guid}/timeseries", async (
            Guid workspaceId, Guid probeId,
            string? window, string? bucket,
            ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;

            var windowSpan = ParseWindow(window ?? "24h");
            var bucketSpan = ParseBucket(bucket ?? "5m");
            var bucketMinutes = (int)bucketSpan.TotalMinutes;
            if (bucketMinutes < 1) { bucketMinutes = 1; }

            var from = DateTimeOffset.UtcNow - windowSpan;

            var sql = $$"""
                SELECT
                    date_trunc('minute', "CompletedAt") -
                        (EXTRACT(MINUTE FROM "CompletedAt")::int % {{bucketMinutes}}) * interval '1 minute'
                        AS "Bucket",
                    COUNT(*) FILTER (WHERE "Outcome" = 'Ok')      AS "Ok",
                    COUNT(*) FILTER (WHERE "Outcome" = 'Warn')    AS "Warn",
                    COUNT(*) FILTER (WHERE "Outcome" = 'Crit')    AS "Crit",
                    COUNT(*) FILTER (WHERE "Outcome" = 'Error')   AS "Error",
                    COUNT(*) FILTER (WHERE "Outcome" = 'Unknown') AS "Unknown",
                    COUNT(*)                                        AS "Total",
                    AVG("DurationMs")                              AS "AvgDurationMs"
                FROM "ProbeResults"
                WHERE "ProbeId" = {0}
                  AND "WorkspaceId" = {1}
                  AND "CompletedAt" >= {2}
                GROUP BY "Bucket"
                ORDER BY "Bucket"
                """;

            var rows = await ctx.Database
                .SqlQueryRaw<TimeseriesRow>(sql, probeId, workspaceId, from)
                .ToListAsync();

            var result = rows.Select(r => new
            {
                bucket = r.Bucket,
                ok = r.Ok,
                warn = r.Warn,
                crit = r.Crit,
                error = r.Error,
                unknown = r.Unknown,
                total = r.Total,
                avgDurationMs = r.AvgDurationMs,
            });

            return Results.Ok(result);
        });

        return app;
    }

    private static TimeSpan ParseWindow(string w) => w switch
    {
        "1h" => TimeSpan.FromHours(1),
        "6h" => TimeSpan.FromHours(6),
        "7d" => TimeSpan.FromDays(7),
        _ => TimeSpan.FromHours(24),
    };

    private static TimeSpan ParseBucket(string b) => b switch
    {
        "1m" => TimeSpan.FromMinutes(1),
        "15m" => TimeSpan.FromMinutes(15),
        "1h" => TimeSpan.FromHours(1),
        _ => TimeSpan.FromMinutes(5),
    };

    private sealed class TimeseriesRow
    {
        public DateTimeOffset Bucket { get; set; }
        public long Ok { get; set; }
        public long Warn { get; set; }
        public long Crit { get; set; }
        public long Error { get; set; }
        public long Unknown { get; set; }
        public long Total { get; set; }
        public double AvgDurationMs { get; set; }
    }

    private static object ToDto(Core.ProbeResult r) => new
    {
        r.Id,
        r.WorkspaceId,
        r.ProbeId,
        r.Outcome,
        r.Message,
        r.DurationMs,
        r.StartedAt,
        r.CompletedAt,
        r.Attributes,
    };
}
