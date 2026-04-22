using Microsoft.EntityFrameworkCore;
using Scry.Api;
using Scry.Data;
using Scry.Probes;
using Scry.Runner;

var builder = WebApplication.CreateBuilder(args);

// ─── Data ────────────────────────────────────────────────────────────────────
var dbPath = builder.Configuration["Scry:DatabasePath"]
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "scry", "scry.db");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContextFactory<ScryDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScryJobQueue();

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

// ─── REST API ────────────────────────────────────────────────────────────────
builder.Services.AddScryApi();

// ─── HTTP ────────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Apply pending migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ScryDbContext>>();
    await using var ctx = await db.CreateDbContextAsync();
    await ctx.Database.MigrateAsync();
}

app.MapScryApi();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));

await app.RunAsync();
