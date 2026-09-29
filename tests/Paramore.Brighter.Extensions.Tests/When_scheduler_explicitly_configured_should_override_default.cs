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

namespace Paramore.Brighter.Extensions.Tests;

public class When_scheduler_explicitly_configured_should_override_default
{
    [Test]
    [Arguments(RequestSchedulerType.Send, false, false)]
    [Arguments(RequestSchedulerType.Send, false, true)]
    [Arguments(RequestSchedulerType.Send, true, false)]
    [Arguments(RequestSchedulerType.Send, true, true)]
    [Arguments(RequestSchedulerType.Publish, false, false)]
    [Arguments(RequestSchedulerType.Publish, false, true)]
    [Arguments(RequestSchedulerType.Publish, true, false)]
    [Arguments(RequestSchedulerType.Publish, true, true)]
    [Arguments(RequestSchedulerType.Post, false, false)]
    [Arguments(RequestSchedulerType.Post, false, true)]
    [Arguments(RequestSchedulerType.Post, true, false)]
    [Arguments(RequestSchedulerType.Post, true, true)]
    public async Task When_scheduling_with_a_legacy_scheduler_should_keep_using_its_existing_contract(
        RequestSchedulerType operation, bool isAsync, bool useDateTime)
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddBrighter().UseScheduler(new StubSchedulerFactory());
        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var request = new TestDoubles.DefaultMapperEvent();
        var context = new RequestContext();
        context.Bag["custom-value"] = 42;
        var delay = TimeSpan.FromSeconds(1);
        var at = DateTimeOffset.UtcNow.Add(delay);

        //Act
        var id = (operation, isAsync, useDateTime) switch
        {
            (RequestSchedulerType.Send, false, false) => processor.Send(delay, request, context),
            (RequestSchedulerType.Send, false, true) => processor.Send(at, request, context),
            (RequestSchedulerType.Send, true, false) => await processor.SendAsync(delay, request, context),
            (RequestSchedulerType.Send, true, true) => await processor.SendAsync(at, request, context),
            (RequestSchedulerType.Publish, false, false) => processor.Publish(delay, request, context),
            (RequestSchedulerType.Publish, false, true) => processor.Publish(at, request, context),
            (RequestSchedulerType.Publish, true, false) => await processor.PublishAsync(delay, request, context),
            (RequestSchedulerType.Publish, true, true) => await processor.PublishAsync(at, request, context),
            (RequestSchedulerType.Post, false, false) => processor.Post(delay, request, context),
            (RequestSchedulerType.Post, false, true) => processor.Post(at, request, context),
            (RequestSchedulerType.Post, true, false) => await processor.PostAsync(delay, request, context),
            (RequestSchedulerType.Post, true, true) => await processor.PostAsync(at, request, context),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        //Assert
        await Assert.That(id).IsEqualTo("stub");
    }

    [Test]
    public async System.Threading.Tasks.Task Should_resolve_custom_factory_instead_of_InMemorySchedulerFactory()
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
        await Assert.That(resolvedFactory).IsNotNull();
        await Assert.That(resolvedFactory).IsTypeOf<StubSchedulerFactory>();
        await Assert.That(resolvedFactory).IsSameReferenceAs(customFactory);
    }

    [Test]
    public async Task Should_resolve_scheduler_from_custom_factory()
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
        await Assert.That(scheduler).IsNotNull();
        await Assert.That(scheduler).IsTypeOf<StubMessageScheduler>();
    }

    [Test]
    public async Task Should_resolve_custom_request_scheduler_factory()
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
        await Assert.That(resolvedFactory).IsNotNull();
        await Assert.That(resolvedFactory).IsTypeOf<StubSchedulerFactory>();
        await Assert.That(resolvedFactory).IsSameReferenceAs(customFactory);
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
