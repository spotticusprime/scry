using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Scry.Core;
using Scry.Probes.Configs;
using Scry.Probes.Internal;

namespace Scry.Probes.Executors;

internal sealed class SqlKpiProbeExecutor : IProbeExecutor
{
    public string Kind => "sql_kpi";

    private readonly IConfiguration _configuration;

    public SqlKpiProbeExecutor(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<ProbeResult> ExecuteAsync(Probe probe, CancellationToken ct)
    {
        var config = YamlConfig.Deserialize<SqlKpiProbeConfig>(probe.Definition);
        var started = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        // Connection string lives in appsettings, not in the probe definition.
        var connStr = _configuration[$"Scry:ConnectionStrings:{config.ConnectionStringName}"];
        if (string.IsNullOrWhiteSpace(connStr))
        {
            sw.Stop();
            return Fail(probe, started, sw, ProbeOutcome.Error,
                $"Connection string 'Scry:ConnectionStrings:{config.ConnectionStringName}' is not configured — add it to appsettings.json or an environment variable");
        }

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(config.Timeout);

        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(probeCts.Token);

            await using var cmd = new SqlCommand(config.Query, conn)
            {
                CommandTimeout = (int)config.Timeout.TotalSeconds,
            };
            var scalar = await cmd.ExecuteScalarAsync(probeCts.Token);
            sw.Stop();

            var label = string.IsNullOrWhiteSpace(config.Description)
                ? config.ConnectionStringName
                : config.Description;

            if (scalar is null or DBNull)
            {
                return Fail(probe, started, sw, ProbeOutcome.Crit,
                    $"{label}: query returned no rows");
            }

            // DateTime result — check age
            if (scalar is DateTime dt || (scalar is string s && DateTime.TryParse(s, out dt)))
            {
                var utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                var ageMinutes = (DateTimeOffset.UtcNow - utc).TotalMinutes;
                var attrs = new Dictionary<string, string>
                {
                    ["value"] = utc.ToString("O"),
                    ["age_minutes"] = $"{ageMinutes:F1}",
                    ["query"] = config.Query,
                };

                if (config.CritAgeMinutes.HasValue && ageMinutes >= config.CritAgeMinutes.Value)
                {
                    return Result(probe, started, sw, ProbeOutcome.Crit,
                        $"{label}: {ageMinutes:F0} min ago (crit threshold: {config.CritAgeMinutes} min)", attrs);
                }
                if (config.WarnAgeMinutes.HasValue && ageMinutes >= config.WarnAgeMinutes.Value)
                {
                    return Result(probe, started, sw, ProbeOutcome.Warn,
                        $"{label}: {ageMinutes:F0} min ago (warn threshold: {config.WarnAgeMinutes} min)", attrs);
                }
                return Result(probe, started, sw, ProbeOutcome.Ok,
                    $"{label}: {ageMinutes:F0} min ago", attrs);
            }

            // Numeric result — check count thresholds
            if (double.TryParse(scalar.ToString(), out var count))
            {
                var attrs = new Dictionary<string, string>
                {
                    ["value"] = count.ToString("G"),
                    ["query"] = config.Query,
                };
                if (config.CritBelowCount.HasValue && count < config.CritBelowCount.Value)
                {
                    return Result(probe, started, sw, ProbeOutcome.Crit,
                        $"{label}: count={count} < crit threshold {config.CritBelowCount}", attrs);
                }
                if (config.WarnBelowCount.HasValue && count < config.WarnBelowCount.Value)
                {
                    return Result(probe, started, sw, ProbeOutcome.Warn,
                        $"{label}: count={count} < warn threshold {config.WarnBelowCount}", attrs);
                }
                return Result(probe, started, sw, ProbeOutcome.Ok,
                    $"{label}: {count}", attrs);
            }

            // Anything else — just report Ok with the raw value
            return Result(probe, started, sw, ProbeOutcome.Ok,
                $"{label}: {scalar}",
                new Dictionary<string, string> { ["value"] = scalar.ToString()!, ["query"] = config.Query });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return Fail(probe, started, sw, ProbeOutcome.Error,
                $"SQL query timed out after {config.Timeout.TotalSeconds:0}s");
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return Fail(probe, started, sw, ProbeOutcome.Error, $"SQL error: {ex.Message}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Fail(probe, started, sw, ProbeOutcome.Error, ex.Message);
        }
    }

    private static ProbeResult Fail(Probe probe, DateTimeOffset started, Stopwatch sw,
        ProbeOutcome outcome, string message) =>
        Result(probe, started, sw, outcome, message, []);

    private static ProbeResult Result(Probe probe, DateTimeOffset started, Stopwatch sw,
        ProbeOutcome outcome, string message, Dictionary<string, string> attrs) => new()
    {
        WorkspaceId = probe.WorkspaceId,
        ProbeId = probe.Id,
        Outcome = outcome,
        Message = message,
        DurationMs = sw.ElapsedMilliseconds,
        StartedAt = started,
        CompletedAt = DateTimeOffset.UtcNow,
        Attributes = attrs,
    };
}
