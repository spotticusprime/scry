namespace Scry.Probes.Configs;

internal sealed class SqlKpiProbeConfig
{
    // References Scry:ConnectionStrings:{Name} in appsettings — keeps secrets out of the DB.
    public required string ConnectionStringName { get; init; }

    // SQL query that returns a single scalar value (typically a DateTime/DateTimeOffset or a count).
    public required string Query { get; init; }

    // Human label shown in probe results, e.g. "Last order placed"
    public string Description { get; init; } = "";

    // If the query returns a datetime: Crit if older than this many minutes.
    public int? CritAgeMinutes { get; init; }

    // If the query returns a datetime: Warn if older than this many minutes.
    public int? WarnAgeMinutes { get; init; }

    // If the query returns a numeric count: Warn/Crit if below threshold.
    public double? WarnBelowCount { get; init; }
    public double? CritBelowCount { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
