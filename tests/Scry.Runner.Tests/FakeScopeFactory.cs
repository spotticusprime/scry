using Microsoft.Extensions.DependencyInjection;
using Scry.Runner;

namespace Scry.Runner.Tests;

internal sealed class FakeScopeFactory : IServiceScopeFactory
{
    private readonly IJobHandler[] _handlers;

    public FakeScopeFactory(params IJobHandler[] handlers) => _handlers = handlers;

    public IServiceScope CreateScope() => new FakeScope(_handlers);

    private sealed class FakeScope(IJobHandler[] handlers) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider(handlers);
        public void Dispose() { }
    }

    // GetServices<IJobHandler>() calls GetService(typeof(IEnumerable<IJobHandler>)).
    private sealed class FakeServiceProvider(IJobHandler[] handlers) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IEnumerable<IJobHandler>) ? handlers : null;
    }
}
