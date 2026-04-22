using Microsoft.Extensions.Logging.Abstractions;
using Scry.Core;

namespace Scry.Runner.Tests;

public class JobDispatcherTests
{
    private static readonly Guid WsId = Guid.NewGuid();

    private static Job MakeJob(string kind) => new()
    {
        WorkspaceId = WsId,
        Kind = kind,
        Payload = "{}",
    };

    private static JobDispatcher MakeDispatcher(FakeJobQueue queue, params IJobHandler[] handlers) =>
        new(queue, new FakeScopeFactory(handlers), "worker-test", TimeSpan.FromMinutes(1), TimeSpan.Zero,
            NullLogger<JobDispatcher>.Instance);

    [Fact]
    public async Task Dispatches_Job_To_Registered_Handler()
    {
        var queue = new FakeJobQueue();
        var handler = new RecordingHandler("ping");
        queue.Enqueue(MakeJob("ping"));

        var dispatcher = MakeDispatcher(queue, handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = dispatcher.StartAsync(cts.Token);

        // Wait on queue completion, not handler start, to avoid the race between
        // HandleAsync returning and CompleteAsync being called.
        await Retry.UntilAsync(() => queue.Completed.Count > 0, cts.Token);
        await cts.CancelAsync();

        Assert.Single(handler.Handled);
        Assert.Single(queue.Completed);
        Assert.Empty(queue.Failed);
    }

    [Fact]
    public async Task Completes_Job_After_Successful_Handle()
    {
        var queue = new FakeJobQueue();
        var job = MakeJob("ping");
        queue.Enqueue(job);
        var handler = new RecordingHandler("ping");

        var dispatcher = MakeDispatcher(queue, handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = dispatcher.StartAsync(cts.Token);

        await Retry.UntilAsync(() => queue.Completed.Count > 0, cts.Token);
        await cts.CancelAsync();

        Assert.Equal(job.Id, queue.Completed[0].JobId);
        Assert.Equal("worker-test", queue.Completed[0].WorkerId);
    }

    [Fact]
    public async Task Fails_Job_When_Handler_Throws()
    {
        var queue = new FakeJobQueue();
        var job = MakeJob("boom");
        queue.Enqueue(job);

        var handler = new ThrowingHandler("boom");
        var dispatcher = MakeDispatcher(queue, handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = dispatcher.StartAsync(cts.Token);

        await Retry.UntilAsync(() => queue.Failed.Count > 0, cts.Token);
        await cts.CancelAsync();

        Assert.Equal(job.Id, queue.Failed[0].JobId);
        Assert.Contains("deliberate", queue.Failed[0].Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(queue.Completed);
    }

    [Fact]
    public async Task Fails_Job_When_No_Handler_Registered()
    {
        var queue = new FakeJobQueue();
        var job = MakeJob("unknown-kind");
        queue.Enqueue(job);

        var dispatcher = MakeDispatcher(queue);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = dispatcher.StartAsync(cts.Token);

        await Retry.UntilAsync(() => queue.Failed.Count > 0, cts.Token);
        await cts.CancelAsync();

        Assert.Equal(job.Id, queue.Failed[0].JobId);
        Assert.Contains("unknown-kind", queue.Failed[0].Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatches_To_Correct_Handler_By_Kind()
    {
        var queue = new FakeJobQueue();
        var pingHandler = new RecordingHandler("ping");
        var pongHandler = new RecordingHandler("pong");
        queue.Enqueue(MakeJob("ping"));
        queue.Enqueue(MakeJob("pong"));
        queue.Enqueue(MakeJob("ping"));

        var dispatcher = MakeDispatcher(queue, pingHandler, pongHandler);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = dispatcher.StartAsync(cts.Token);

        await Retry.UntilAsync(() => queue.Completed.Count == 3, cts.Token);
        await cts.CancelAsync();

        Assert.Equal(2, pingHandler.Handled.Count); // 2 ping jobs
        Assert.Single(pongHandler.Handled);
    }

    [Fact]
    public async Task StartAsync_Throws_On_Duplicate_Handler_Kinds()
    {
        var queue = new FakeJobQueue();
        var dispatcher = MakeDispatcher(queue,
            new RecordingHandler("ping"),
            new RecordingHandler("ping")); // duplicate

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.StartAsync(CancellationToken.None));
    }

    // Helpers

    private sealed class RecordingHandler(string kind) : IJobHandler
    {
        public string Kind => kind;
        public List<Job> Handled { get; } = [];

        public Task HandleAsync(Job job, CancellationToken ct)
        {
            Handled.Add(job);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler(string kind) : IJobHandler
    {
        public string Kind => kind;
        public Task HandleAsync(Job job, CancellationToken ct) =>
            throw new InvalidOperationException("Deliberate test failure");
    }

    private static class Retry
    {
        public static async Task UntilAsync(Func<bool> condition, CancellationToken ct)
        {
            while (!condition() && !ct.IsCancellationRequested)
            {
                await Task.Delay(10, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }

            if (!condition())
            {
                throw new TimeoutException("Test condition was not met before the cancellation token fired.");
            }
        }
    }
}
