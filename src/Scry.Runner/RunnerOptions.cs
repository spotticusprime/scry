namespace Scry.Runner;

public sealed class RunnerOptions
{
    // 16 chars of host context + ":" + 32-char GUID = always unique, always ≤49 chars.
    // MachineName can be up to 253 chars on Linux; naively slicing [..32] would discard the GUID.
    public string WorkerId { get; set; } =
        $"{Environment.MachineName[..Math.Min(Environment.MachineName.Length, 16)]}:{Guid.NewGuid():N}";

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
}
