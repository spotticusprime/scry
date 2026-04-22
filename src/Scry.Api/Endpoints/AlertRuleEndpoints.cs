using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Scry.Core;
using Scry.Data;

namespace Scry.Api.Endpoints;

internal static class AlertRuleEndpoints
{
    internal static IEndpointRouteBuilder MapAlertRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId:guid}/alerts").WithTags("AlertRules");

        group.MapGet("/", async (Guid workspaceId, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var rules = await ctx.AlertRules.OrderBy(r => r.Name).ToListAsync();
            return Results.Ok(rules.Select(ToDto));
        });

        group.MapGet("/{id:guid}", async (Guid workspaceId, Guid id, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var r = await ctx.AlertRules.FirstOrDefaultAsync(r => r.Id == id);
            return r is null ? Results.NotFound() : Results.Ok(ToDto(r));
        });

        group.MapPost("/", async (Guid workspaceId, CreateAlertRuleRequest req, ScryDbContext ctx) =>
        {
            var rule = new AlertRule
            {
                WorkspaceId = workspaceId,
                Name = req.Name,
                // Phase 1 expression: comma-separated ProbeOutcome names, e.g. "Warn,Crit"
                Expression = req.Expression,
                Severity = Enum.Parse<AlertSeverity>(req.Severity, ignoreCase: true),
                ProbeIdFilter = req.ProbeIdFilter,
                NotifierConfig = req.NotifierConfig,
            };
            ctx.AlertRules.Add(rule);
            await ctx.SaveChangesAsync();
            return Results.Created($"/api/workspaces/{workspaceId}/alerts/{rule.Id}", ToDto(rule));
        });

        group.MapPut("/{id:guid}", async (Guid workspaceId, Guid id, UpdateAlertRuleRequest req, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var rule = await ctx.AlertRules.FirstOrDefaultAsync(r => r.Id == id);
            if (rule is null)
            {
                return Results.NotFound();
            }
            rule.Name = req.Name ?? rule.Name;
            rule.Expression = req.Expression ?? rule.Expression;
            rule.Enabled = req.Enabled ?? rule.Enabled;
            rule.NotifierConfig = req.NotifierConfig ?? rule.NotifierConfig;
            if (req.Severity is not null)
            {
                rule.Severity = Enum.Parse<AlertSeverity>(req.Severity, ignoreCase: true);
            }
            await ctx.SaveChangesAsync();
            return Results.Ok(ToDto(rule));
        });

        group.MapDelete("/{id:guid}", async (Guid workspaceId, Guid id, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var rule = await ctx.AlertRules.FirstOrDefaultAsync(r => r.Id == id);
            if (rule is null)
            {
                return Results.NotFound();
            }
            ctx.AlertRules.Remove(rule);
            await ctx.SaveChangesAsync();
            return Results.NoContent();
        });

        // Read alert events (firing/resolved history) for a rule.
        group.MapGet("/{id:guid}/events", async (Guid workspaceId, Guid id, ScryDbContext ctx) =>
        {
            ctx.CurrentWorkspaceId = workspaceId;
            var events = await ctx.AlertEvents
                .Where(e => e.AlertRuleId == id)
                .OrderByDescending(e => e.OpenedAt)
                .Take(100)
                .ToListAsync();
            return Results.Ok(events.Select(e => new
            {
                e.Id,
                e.Fingerprint,
                e.State,
                e.Severity,
                e.Summary,
                e.OpenedAt,
                e.ResolvedAt,
                e.LastNotifiedAt,
            }));
        });

        return app;
    }

    private static object ToDto(AlertRule r) => new
    {
        r.Id,
        r.WorkspaceId,
        r.Name,
        r.Expression,
        Severity = r.Severity.ToString(),
        r.Enabled,
        r.ProbeIdFilter,
        r.NotifierConfig,
        r.CreatedAt,
        r.UpdatedAt,
    };

    internal sealed record CreateAlertRuleRequest(
        string Name,
        string Expression,
        string Severity,
        Guid? ProbeIdFilter,
        string? NotifierConfig);

    internal sealed record UpdateAlertRuleRequest(
        string? Name,
        string? Expression,
        string? Severity,
        bool? Enabled,
        string? NotifierConfig);
}
