namespace Scry.Runner;

public sealed class RunnerOptions
{
    public string WorkerId { get; set; } = $"{Environment.MachineName}:{Guid.NewGuid():N}"[..32];
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
}
