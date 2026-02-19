#region Licence
/* The MIT License (MIT)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class When_scheduler_explicitly_configured_should_override_default
{
    [Fact]
    public void Should_resolve_custom_factory_instead_of_InMemorySchedulerFactory()
    {
        // Arrange — configure a custom scheduler factory via UseScheduler
        var customFactory = new StubSchedulerFactory();

        var services = new ServiceCollection();
        services.AddBrighter()
            .UseScheduler(customFactory);
        var provider = services.BuildServiceProvider();

        // Act
        var resolvedFactory = provider.GetRequiredService<IAmAMessageSchedulerFactory>();

        // Assert — the custom factory should be resolved, not the default InMemorySchedulerFactory
        Assert.NotNull(resolvedFactory);
        Assert.IsType<StubSchedulerFactory>(resolvedFactory);
        Assert.Same(customFactory, resolvedFactory);
    }

    [Fact]
    public void Should_resolve_scheduler_from_custom_factory()
    {
        // Arrange — configure a custom scheduler factory via UseScheduler
        var customFactory = new StubSchedulerFactory();

        var services = new ServiceCollection();
        services.AddBrighter()
            .UseScheduler(customFactory);
        var provider = services.BuildServiceProvider();

        // Act
        var scheduler = provider.GetRequiredService<IAmAMessageScheduler>();

        // Assert — the scheduler should come from the custom factory
        Assert.NotNull(scheduler);
        Assert.IsType<StubMessageScheduler>(scheduler);
    }

    [Fact]
    public void Should_resolve_custom_request_scheduler_factory()
    {
        // Arrange — configure a custom scheduler factory via UseScheduler
        var customFactory = new StubSchedulerFactory();

        var services = new ServiceCollection();
        services.AddBrighter()
            .UseScheduler(customFactory);
        var provider = services.BuildServiceProvider();

        // Act
        var resolvedFactory = provider.GetRequiredService<IAmARequestSchedulerFactory>();

        // Assert — the custom factory should be resolved for request scheduling too
        Assert.NotNull(resolvedFactory);
        Assert.IsType<StubSchedulerFactory>(resolvedFactory);
        Assert.Same(customFactory, resolvedFactory);
    }

    private class StubSchedulerFactory : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
    {
        public IAmAMessageScheduler Create(IAmACommandProcessor processor) => new StubMessageScheduler();
        public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor) => new StubRequestScheduler();
        public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor) => new StubRequestScheduler();
    }

    private class StubMessageScheduler : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync
    {
        public string Schedule(Message message, DateTimeOffset at) => "stub";
        public string Schedule(Message message, TimeSpan delay) => "stub";
        public bool ReScheduler(string schedulerId, DateTimeOffset at) => false;
        public bool ReScheduler(string schedulerId, TimeSpan delay) => false;
        public void Cancel(string id) { }
        public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default) => Task.FromResult("stub");
        public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default) => Task.FromResult("stub");
        public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task CancelAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class StubRequestScheduler : IAmARequestSchedulerSync, IAmARequestSchedulerAsync
    {
        public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at) where TRequest : class, IRequest => "stub";
        public string Schedule<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay) where TRequest : class, IRequest => "stub";
        public bool ReScheduler(string schedulerId, DateTimeOffset at) => false;
        public bool ReScheduler(string schedulerId, TimeSpan delay) => false;
        public void Cancel(string id) { }
        public Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, DateTimeOffset at, CancellationToken cancellationToken = default) where TRequest : class, IRequest => Task.FromResult("stub");
        public Task<string> ScheduleAsync<TRequest>(TRequest request, RequestSchedulerType type, TimeSpan delay, CancellationToken cancellationToken = default) where TRequest : class, IRequest => Task.FromResult("stub");
        public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task CancelAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
