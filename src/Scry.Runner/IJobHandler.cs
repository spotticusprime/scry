using Scry.Core;

namespace Scry.Runner;

public interface IJobHandler
{
    string Kind { get; }
    Task HandleAsync(Job job, CancellationToken ct);
}
