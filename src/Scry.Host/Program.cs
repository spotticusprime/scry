using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ApexCharts;
using Scry.Api;
using Scry.Core;
using Scry.Data;
using Scry.Host.Components;
using Scry.Host.Hubs;
using Scry.Probes;
using Scry.Runner;

var builder = WebApplication.CreateBuilder(args);

// appsettings.Local.json is gitignored — put real connection strings and passwords there.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

// ─── Data ────────────────────────────────────────────────────────────────────
var pgConn = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
var myConn = builder.Configuration.GetConnectionString("MySql")
    ?? throw new InvalidOperationException("ConnectionStrings:MySql is required.");

builder.Services.AddDbContextFactory<ScryDbContext>(opt => opt.UseNpgsql(pgConn));
builder.Services.AddDbContextFactory<ScryJobDbContext>(opt => opt.UseMySQL(myConn));

builder.Services.AddScryJobQueue();
builder.Services.AddScryData();

// ─── Probes & alerts ─────────────────────────────────────────────────────────
builder.Services.AddScryProbes();

// ─── Job runner ──────────────────────────────────────────────────────────────
builder.Services.AddScryRunner(opt =>
{
    var section = builder.Configuration.GetSection("Scry:Runner");
    if (section["PollInterval"] is { } poll)
    {
        opt.PollInterval = TimeSpan.Parse(poll);
    }
    if (section["LeaseDuration"] is { } lease)
    {
        opt.LeaseDuration = TimeSpan.Parse(lease);
    }
});

// ─── SignalR ─────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddSingleton<IProbeResultPublisher, SignalRProbeResultPublisher>();

// ─── REST API ────────────────────────────────────────────────────────────────
builder.Services.AddScryApi();

// ─── Authentication (cookie) ─────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/login";
        opt.LogoutPath = "/auth/logout";
        opt.ExpireTimeSpan = TimeSpan.FromDays(7);
        opt.SlidingExpiration = true;
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ─── Blazor ──────────────────────────────────────────────────────────────────
// PHASE 1: Single-node only. Blazor Interactive Server requires sticky sessions; scale-out
// needs a Redis SignalR backplane and session affinity at the load balancer.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddApexCharts();

// ─── HTTP ────────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddAntiforgery();


var app = builder.Build();

// Apply pending migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var pgFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ScryDbContext>>();
    await using var pgCtx = await pgFactory.CreateDbContextAsync();
    await pgCtx.Database.MigrateAsync();

    var myFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ScryJobDbContext>>();
    await using var myCtx = await myFactory.CreateDbContextAsync();
    await myCtx.Database.MigrateAsync();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ─── Auth endpoints ───────────────────────────────────────────────────────────
app.MapPost("/auth/login", async (HttpContext ctx, IConfiguration config) =>
{
    var form = ctx.Request.Form;
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var expectedUser = config["Scry:Auth:Username"] ?? "admin";
    var expectedPass = config["Scry:Auth:Password"];

    if (string.IsNullOrWhiteSpace(expectedPass))
    {
        // No password configured — bounce back with a clear error.
        return Results.Redirect("/login?error=2");
    }

    if (!string.Equals(username, expectedUser, StringComparison.Ordinal)
        || !string.Equals(password, expectedPass, StringComparison.Ordinal))
    {
        return Results.Redirect("/login?error=1");
    }

    var claims = new[] { new Claim(ClaimTypes.Name, username!) };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));

    // Only allow local redirects to prevent open redirect attacks.
    var redirect = returnUrl is { Length: > 0 } r && r.StartsWith('/') && !r.StartsWith("//") ? r : "/";
    return Results.Redirect(redirect);
}).AllowAnonymous();

app.MapGet("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous();

// ─── Demo seed ────────────────────────────────────────────────────────────────
app.MapPost("/api/demo/seed", async (IDbContextFactory<ScryDbContext> factory, IJobQueue jobQueue) =>
{
    await using var ctx = await factory.CreateDbContextAsync();
    var initialJobs = new List<Job>();

    // Idempotent — skip if demo workspaces already exist.
    if (await ctx.Workspaces.AnyAsync(w => w.Name == "Work Demo" || w.Name == "Personal"))
    {
        return Results.Ok(new { message = "Demo data already seeded." });
    }

    // ── Personal workspace ────────────────────────────────────────────────────
    var personal = new Workspace { Name = "Personal", Description = "Personal infrastructure — sc.co.gg" };
    ctx.Workspaces.Add(personal);

    var personalProbes = new[]
    {
        ("sc.co.gg — HTTPS", "http",
         "url: https://sc.co.gg\nexpected_status: 200\ntimeout: 00:00:15"),
        ("sc.co.gg — TLS cert", "tls",
         "host: sc.co.gg\nwarn_days: 30\ncrit_days: 7"),
        ("sc.co.gg — DNS", "dns",
         "host: sc.co.gg\ntimeout: 00:00:10"),
        ("sali.cloud — HTTPS", "http",
         "url: https://sali.cloud\ntimeout: 00:00:15"),
        ("sali.cloud — TLS cert", "tls",
         "host: sali.cloud\nwarn_days: 30\ncrit_days: 7"),
    };

    foreach (var (name, kind, def) in personalProbes)
    {
        var probe = new Probe
        {
            WorkspaceId = personal.Id,
            Name = name,
            Kind = kind,
            Definition = def,
            Interval = TimeSpan.FromMinutes(5),
        };
        ctx.Probes.Add(probe);
        initialJobs.Add(ScryProbesExtensions.CreateInitialProbeJob(probe));
    }

    // ── Work Demo workspace ───────────────────────────────────────────────────
    var work = new Workspace { Name = "Work Demo", Description = "Azure infrastructure + external services" };
    ctx.Workspaces.Add(work);

    var workProbes = new[]
    {
        // External payment / shipping gateways
        ("Braintree — gateway status", "http",
         "url: https://status.braintreepayments.com/\nexpected_status: 200\nbody_contains: Braintree\ntimeout: 00:00:15"),
        ("PayPal — service health", "http",
         "url: https://www.paypal-status.com/\nexpected_status: 200\ntimeout: 00:00:15"),
        ("FedEx — service status", "http",
         "url: https://www.fedex.com/en-us/home.html\nexpected_status: 200\ntimeout: 00:00:20"),
        ("USPS — site availability", "http",
         "url: https://www.usps.com/\nexpected_status: 200\ntimeout: 00:00:15"),

        // Azure App Service (will return Error with helpful message until credentials added)
        ("App Service — CPU %", "azure_metric",
         "subscription_id: \"<your-subscription-id>\"\nresource_group: \"<resource-group>\"\nresource_name: \"<app-service-name>\"\nresource_type: Microsoft.Web/sites\ntime_window_minutes: 15\naggregation: Average\nmetrics:\n  - name: CpuPercentage\n    warn_threshold: 70\n    crit_threshold: 90\n    unit: \"%\"\n  - name: Http5xx\n    warn_threshold: 10\n    crit_threshold: 50\n    unit: \" req\""),
        ("App Service — Memory %", "azure_metric",
         "subscription_id: \"<your-subscription-id>\"\nresource_group: \"<resource-group>\"\nresource_name: \"<app-service-name>\"\nresource_type: Microsoft.Web/sites\ntime_window_minutes: 15\naggregation: Average\nmetrics:\n  - name: MemoryWorkingSet\n    warn_threshold: 1500000000\n    crit_threshold: 1800000000\n    unit: \" B\""),
        ("VM — CPU utilization", "azure_metric",
         "subscription_id: \"<your-subscription-id>\"\nresource_group: \"<resource-group>\"\nresource_name: \"<vm-name>\"\nresource_type: Microsoft.Compute/virtualMachines\ntime_window_minutes: 15\naggregation: Average\nmetrics:\n  - name: Percentage CPU\n    warn_threshold: 80\n    crit_threshold: 95\n    unit: \"%\""),

        // SQL KPI probes (will return Error with helpful message until connection strings added)
        ("SGW — Last order", "sql_kpi",
         "connection_string_name: SgwProd\nquery: \"SELECT TOP 1 CreatedAt FROM Orders ORDER BY CreatedAt DESC\"\ndescription: Last order placed\nwarn_age_minutes: 30\ncrit_age_minutes: 60"),
        ("SGW — Last bid", "sql_kpi",
         "connection_string_name: SgwProd\nquery: \"SELECT TOP 1 CreatedAt FROM Bids ORDER BY CreatedAt DESC\"\ndescription: Last bid placed\nwarn_age_minutes: 60\ncrit_age_minutes: 120"),
        ("SGW — Last user registration", "sql_kpi",
         "connection_string_name: SgwProd\nquery: \"SELECT TOP 1 CreatedAt FROM Users ORDER BY CreatedAt DESC\"\ndescription: Last user registration\nwarn_age_minutes: 1440\ncrit_age_minutes: 4320"),
    };

    foreach (var (name, kind, def) in workProbes)
    {
        var probe = new Probe
        {
            WorkspaceId = work.Id,
            Name = name,
            Kind = kind,
            Definition = def,
            Interval = TimeSpan.FromMinutes(5),
        };
        ctx.Probes.Add(probe);
        initialJobs.Add(ScryProbesExtensions.CreateInitialProbeJob(probe));
    }

    // ── Alert rules for Work Demo ─────────────────────────────────────────────
    ctx.AlertRules.Add(new AlertRule
    {
        WorkspaceId = work.Id,
        Name = "External gateway down",
        Expression = "Crit,Error",
        Severity = AlertSeverity.Critical,
    });
    ctx.AlertRules.Add(new AlertRule
    {
        WorkspaceId = work.Id,
        Name = "Azure resource degraded",
        Expression = "Warn,Crit,Error",
        Severity = AlertSeverity.Warning,
    });
    ctx.AlertRules.Add(new AlertRule
    {
        WorkspaceId = work.Id,
        Name = "KPI stale — critical",
        Expression = "Crit",
        Severity = AlertSeverity.Critical,
    });

    await ctx.SaveChangesAsync();

    foreach (var j in initialJobs)
    {
        await jobQueue.EnqueueAsync(j);
    }

    return Results.Ok(new
    {
        message = "Demo data seeded.",
        personalWorkspaceId = personal.Id,
        workWorkspaceId = work.Id,
    });
}).RequireAuthorization();

// ─── SignalR hub ──────────────────────────────────────────────────────────────
app.MapHub<ProbeHub>("/hubs/probes").RequireAuthorization();

// ─── REST API ─────────────────────────────────────────────────────────────────
app.MapScryApi();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }))
    .AllowAnonymous();

// ─── Blazor ───────────────────────────────────────────────────────────────────
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
