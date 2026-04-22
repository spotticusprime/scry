using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scry.Core;

namespace Scry.Runner;

internal sealed class JobDispatcher : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _workerId;
    private readonly TimeSpan _leaseDuration;
    private readonly TimeSpan _pollInterval;
    private readonly ILogger<JobDispatcher> _logger;

    internal JobDispatcher(
        IJobQueue queue,
        IServiceScopeFactory scopeFactory,
        string workerId,
        TimeSpan leaseDuration,
        TimeSpan pollInterval,
        ILogger<JobDispatcher> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _workerId = workerId;
        _leaseDuration = leaseDuration;
        _pollInterval = pollInterval;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Fail fast on duplicate handler registrations — a silent ArgumentException from
        // ToDictionary at first dispatch is much harder to diagnose.
        using var scope = _scopeFactory.CreateScope();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handler in scope.ServiceProvider.GetServices<IJobHandler>())
        {
            if (!seen.Add(handler.Kind))
            {
                throw new InvalidOperationException(
                    $"Multiple IJobHandler registrations for job kind '{handler.Kind}'. Each kind must have exactly one handler.");
            }
        }
        await base.StartAsync(cancellationToken);
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

            // Create a new scope per job so handlers can safely inject scoped services
            // (e.g. IDbContextFactory, per-request state) without lifetime issues.
            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider
                .GetServices<IJobHandler>()
                .FirstOrDefault(h => StringComparer.Ordinal.Equals(h.Kind, job.Kind));

            if (handler is null)
            {
                _logger.LogWarning("No handler registered for job kind '{Kind}' (jobId={JobId})", job.Kind, job.Id);
                await _queue.FailAsync(job.Id, _workerId, $"No handler for job kind '{job.Kind}'",
                    _pollInterval, stoppingToken);
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
                await _queue.FailAsync(job.Id, _workerId, ex.Message, _pollInterval, stoppingToken);
            }
        }
    }
}
