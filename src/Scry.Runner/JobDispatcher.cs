using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scry.Core;

namespace Scry.Runner;

internal sealed class JobDispatcher : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IReadOnlyDictionary<string, IJobHandler> _handlers;
    private readonly string _workerId;
    private readonly TimeSpan _leaseDuration;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger<JobDispatcher> _logger;

    internal JobDispatcher(
        IJobQueue queue,
        IEnumerable<IJobHandler> handlers,
        string workerId,
        TimeSpan leaseDuration,
        TimeSpan pollInterval,
        ILogger<JobDispatcher> logger)
    {
        _queue = queue;
        _handlers = handlers.ToDictionary(h => h.Kind, StringComparer.Ordinal);
        _workerId = workerId;
        _leaseDuration = leaseDuration;
        _pollInterval = pollInterval;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Job? job;
            try
            {
                job = await _queue.ClaimAnyAsync(_workerId, _leaseDuration, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (job is null)
            {
                await Task.Delay(_pollInterval, stoppingToken)
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                continue;
            }

            if (!_handlers.TryGetValue(job.Kind, out var handler))
            {
                _logger.LogWarning("No handler registered for job kind '{Kind}' (jobId={JobId})", job.Kind, job.Id);
                await _queue.FailAsync(job.Id, _workerId, $"No handler for job kind '{job.Kind}'",
                    TimeSpan.Zero, stoppingToken);
                continue;
            }

            try
            {
                await handler.HandleAsync(job, stoppingToken);
                await _queue.CompleteAsync(job.Id, _workerId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown while a job was running — lease will expire and the reaper will reclaim it.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler for '{Kind}' faulted (jobId={JobId})", job.Kind, job.Id);
                await _queue.FailAsync(job.Id, _workerId, ex.Message, TimeSpan.Zero, stoppingToken);
            }
        }
    }
}
