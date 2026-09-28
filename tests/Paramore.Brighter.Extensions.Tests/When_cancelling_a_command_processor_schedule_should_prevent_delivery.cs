#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection("CommandProcessor")]
public class ScheduledRequestControlTests
{
    public static TheoryData<RequestSchedulerType, bool, bool, Type> ControlCases
    {
        get
        {
            var cases = new TheoryData<RequestSchedulerType, bool, bool, Type>();
            foreach (var operation in new[] { RequestSchedulerType.Send, RequestSchedulerType.Publish, RequestSchedulerType.Post })
                foreach (var isAsync in new[] { false, true })
                    foreach (var useDateTime in new[] { false, true })
                        foreach (var schedulerType in new[]
                                 {
                                     typeof(IAmARequestSchedulerSync), typeof(IAmARequestSchedulerAsync),
                                     typeof(IAmAMessageSchedulerSync), typeof(IAmAMessageSchedulerAsync)
                                 })
                            cases.Add(operation, isAsync, useDateTime, schedulerType);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ControlCases))]
    public async Task When_cancelling_a_command_processor_schedule_should_prevent_delivery(
        RequestSchedulerType operation, bool isAsync, bool useDateTime, Type schedulerType)
    {
        //Arrange
        await using var setup = new SchedulingSetup();
        var delay = TimeSpan.FromSeconds(10);
        var cancelled = await setup.Schedule(operation, isAsync, useDateTime, delay);
        var control = await setup.Schedule(operation, isAsync, useDateTime, delay);
        Assert.Empty(setup.Delivered);

        //Act
        await setup.Cancel(cancelled.SchedulerId, schedulerType);
        setup.Clock.Advance(delay);

        //Assert
        Assert.Equal(control.RequestId, Assert.Single(setup.Delivered));
        setup.Clock.Advance(delay);
        Assert.Equal(control.RequestId, Assert.Single(setup.Delivered));
    }

    [Theory]
    [MemberData(nameof(ControlCases))]
    public async Task When_rescheduling_a_command_processor_schedule_should_move_delivery(
        RequestSchedulerType operation, bool isAsync, bool useDateTime, Type schedulerType)
    {
        //Arrange
        await using var setup = new SchedulingSetup();
        var scheduled = await setup.Schedule(operation, isAsync, useDateTime, TimeSpan.FromSeconds(10));
        setup.Clock.Advance(TimeSpan.FromSeconds(5));

        //Act
        var rescheduled = await setup.Reschedule(
            scheduled.SchedulerId, schedulerType, useDateTime, TimeSpan.FromSeconds(20));

        //Assert
        Assert.True(rescheduled);
        setup.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Empty(setup.Delivered);
        setup.Clock.Advance(TimeSpan.FromSeconds(14));
        Assert.Empty(setup.Delivered);
        setup.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(scheduled.RequestId, Assert.Single(setup.Delivered));
        Assert.False(await setup.Reschedule(
            scheduled.SchedulerId, schedulerType, useDateTime, TimeSpan.FromSeconds(20)));
        setup.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(scheduled.RequestId, Assert.Single(setup.Delivered));
    }

    [Fact]
    public async Task When_scheduling_duplicate_ids_across_factory_calls_should_reject_the_second_request()
    {
        //Arrange
        await using var setup = new SchedulingSetup(new InMemorySchedulerFactory
        {
            TimeProvider = new FakeTimeProvider(),
            GetOrCreateRequestSchedulerId = _ => "duplicate",
            OnConflict = OnSchedulerConflict.Throw
        });
        var first = await setup.Schedule(RequestSchedulerType.Send, false, false, TimeSpan.FromSeconds(10));

        //Act
        var exception = await Record.ExceptionAsync(() =>
            setup.Schedule(RequestSchedulerType.Send, true, false, TimeSpan.FromSeconds(10)));

        //Assert
        Assert.IsType<InvalidOperationException>(exception);
        setup.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(first.RequestId, Assert.Single(setup.Delivered));
    }

    [Fact]
    public async Task When_overwriting_across_factory_calls_should_deliver_only_the_replacement()
    {
        //Arrange
        await using var setup = new SchedulingSetup(new InMemorySchedulerFactory
        {
            TimeProvider = new FakeTimeProvider(),
            GetOrCreateRequestSchedulerId = _ => "replacement",
            OnConflict = OnSchedulerConflict.Overwrite
        });
        var first = await setup.Schedule(RequestSchedulerType.Send, false, false, TimeSpan.FromSeconds(10));

        //Act
        var replacement = await setup.Schedule(RequestSchedulerType.Send, true, false, TimeSpan.FromSeconds(20));

        //Assert
        Assert.Equal(first.SchedulerId, replacement.SchedulerId);
        setup.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Empty(setup.Delivered);
        setup.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(replacement.RequestId, Assert.Single(setup.Delivered));
        setup.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(replacement.RequestId, Assert.Single(setup.Delivered));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_cancelling_should_leave_another_processors_schedule_untouched(bool shareFactory)
    {
        //Arrange
        var clock = new FakeTimeProvider();
        var factory = new InMemorySchedulerFactory
        {
            TimeProvider = clock,
            GetOrCreateRequestSchedulerId = _ => "same-id"
        };
        await using var first = new SchedulingSetup(factory);
        await using var second = new SchedulingSetup(shareFactory ? factory : new InMemorySchedulerFactory
        {
            TimeProvider = clock,
            GetOrCreateRequestSchedulerId = _ => "same-id"
        });
        var cancelled = await first.Schedule(RequestSchedulerType.Send, false, false, TimeSpan.FromSeconds(10));
        var remaining = await second.Schedule(RequestSchedulerType.Send, false, false, TimeSpan.FromSeconds(10));
        Assert.Equal(cancelled.SchedulerId, remaining.SchedulerId);

        //Act
        await first.Cancel(cancelled.SchedulerId, typeof(IAmARequestSchedulerAsync));
        clock.Advance(TimeSpan.FromSeconds(10));

        //Assert
        Assert.Empty(first.Delivered);
        Assert.Equal(remaining.RequestId, Assert.Single(second.Delivered));
    }

    [Fact]
    public async Task When_using_another_factory_should_not_control_the_original_schedule()
    {
        //Arrange
        await using var setup = new SchedulingSetup();
        var otherFactory = new InMemorySchedulerFactory { TimeProvider = setup.Clock };
        var otherScheduler = otherFactory.CreateAsync(setup.Processor);
        await using var otherLifetime = (IAsyncDisposable)otherScheduler;
        var scheduled = await setup.Schedule(RequestSchedulerType.Send, true, false, TimeSpan.FromSeconds(10));

        //Act
        await otherScheduler.CancelAsync(scheduled.SchedulerId);
        var changedByOtherFactory = await otherScheduler.ReSchedulerAsync(scheduled.SchedulerId, TimeSpan.FromSeconds(30));
        var changedByOriginalFactory = await setup.Reschedule(
            scheduled.SchedulerId, typeof(IAmARequestSchedulerAsync), false, TimeSpan.FromSeconds(20));

        //Assert
        Assert.False(changedByOtherFactory);
        Assert.True(changedByOriginalFactory);
        setup.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Empty(setup.Delivered);
        setup.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(scheduled.RequestId, Assert.Single(setup.Delivered));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_disposing_a_registered_scheduler_should_cancel_its_pending_requests(bool isAsync)
    {
        //Arrange
        await using var setup = new SchedulingSetup();
        var scheduled = await setup.Schedule(RequestSchedulerType.Send, isAsync, false, TimeSpan.FromSeconds(10));
        var scheduler = setup.Provider.GetRequiredService<IAmAMessageScheduler>();

        //Act
        if (isAsync)
        {
            await ((IAsyncDisposable)scheduler).DisposeAsync();
            await ((IAsyncDisposable)scheduler).DisposeAsync();
        }
        else
        {
            ((IDisposable)scheduler).Dispose();
            ((IDisposable)scheduler).Dispose();
        }
        setup.Clock.Advance(TimeSpan.FromSeconds(10));

        //Assert
        Assert.Empty(setup.Delivered);
        Assert.False(await setup.Reschedule(
            scheduled.SchedulerId, typeof(IAmARequestSchedulerAsync), false, TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public async Task When_scheduling_concurrently_should_allow_each_request_to_be_cancelled()
    {
        //Arrange
        await using var setup = new SchedulingSetup();
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 16).Select(index => Task.Run(async () =>
        {
            await start.Task;
            return await setup.Schedule(RequestSchedulerType.Send, index % 2 == 0, false, TimeSpan.FromSeconds(10));
        })).ToArray();

        //Act
        start.SetResult(true);
        var schedules = await Task.WhenAll(tasks);
        foreach (var scheduled in schedules)
            await setup.Cancel(scheduled.SchedulerId, typeof(IAmAMessageSchedulerAsync));
        var control = await setup.Schedule(RequestSchedulerType.Send, true, false, TimeSpan.FromSeconds(10));
        setup.Clock.Advance(TimeSpan.FromSeconds(10));

        //Assert
        Assert.Equal(schedules.Length, schedules.Select(scheduled => scheduled.SchedulerId).Distinct().Count());
        Assert.Equal(control.RequestId, Assert.Single(setup.Delivered));
    }

    private sealed class SchedulingSetup : IAsyncDisposable
    {
        private readonly InternalBus _bus = new();
        private readonly RoutingKey _syncTopic = new("scheduler-control.sync");
        private readonly RoutingKey _asyncTopic = new("scheduler-control.async");
        private readonly SchedulerControlEventHandler _syncHandler = new();
        private readonly SchedulerControlEventHandlerAsync _asyncHandler = new();

        public FakeTimeProvider Clock { get; }
        public ServiceProvider Provider { get; }
        public IAmACommandProcessor Processor { get; }
        public Id[] Delivered => _syncHandler.Received.Concat(_asyncHandler.Received)
            .Concat(_bus.Stream(_syncTopic).Select(message => message.Id))
            .Concat(_bus.Stream(_asyncTopic).Select(message => message.Id)).ToArray();

        public SchedulingSetup(InMemorySchedulerFactory? factory = null)
        {
            factory ??= new InMemorySchedulerFactory { TimeProvider = new FakeTimeProvider() };
            Clock = (FakeTimeProvider)factory.TimeProvider;
            var services = new ServiceCollection();
            services.AddSingleton(_syncHandler);
            services.AddSingleton(_asyncHandler);
            services.AddBrighter()
                .UseScheduler(factory)
                .AddProducers(options => options.ProducerRegistry = new InMemoryProducerRegistryFactory(_bus,
                    [
                        new Publication { Topic = _syncTopic, RequestType = typeof(SchedulerControlEvent) },
                        new Publication { Topic = _asyncTopic, RequestType = typeof(SchedulerControlEventAsync) }
                    ], InstrumentationOptions.All).Create())
                .MapperRegistry(registry =>
                {
                    registry.Register<SchedulerControlEvent, JsonMessageMapper<SchedulerControlEvent>>();
                    registry.RegisterAsync<SchedulerControlEventAsync, JsonMessageMapper<SchedulerControlEventAsync>>();
                });
            Provider = services.BuildServiceProvider();
            Processor = Provider.GetRequiredService<IAmACommandProcessor>();
        }

        public Task<(string SchedulerId, Id RequestId)> Schedule(
            RequestSchedulerType operation, bool isAsync, bool useDateTime, TimeSpan delay)
            => isAsync
                ? Schedule(new SchedulerControlEventAsync(), operation, true, useDateTime, delay)
                : Schedule(new SchedulerControlEvent(), operation, false, useDateTime, delay);

        private async Task<(string SchedulerId, Id RequestId)> Schedule<TRequest>(
            TRequest request, RequestSchedulerType operation, bool isAsync, bool useDateTime, TimeSpan delay)
            where TRequest : class, IRequest
        {
            var at = Clock.GetUtcNow().Add(delay);
            var id = (operation, isAsync, useDateTime) switch
            {
                (RequestSchedulerType.Send, false, false) => Processor.Send(delay, request),
                (RequestSchedulerType.Send, false, true) => Processor.Send(at, request),
                (RequestSchedulerType.Send, true, false) => await Processor.SendAsync(delay, request),
                (RequestSchedulerType.Send, true, true) => await Processor.SendAsync(at, request),
                (RequestSchedulerType.Publish, false, false) => Processor.Publish(delay, request),
                (RequestSchedulerType.Publish, false, true) => Processor.Publish(at, request),
                (RequestSchedulerType.Publish, true, false) => await Processor.PublishAsync(delay, request),
                (RequestSchedulerType.Publish, true, true) => await Processor.PublishAsync(at, request),
                (RequestSchedulerType.Post, false, false) => Processor.Post(delay, request),
                (RequestSchedulerType.Post, false, true) => Processor.Post(at, request),
                (RequestSchedulerType.Post, true, false) => await Processor.PostAsync(delay, request),
                (RequestSchedulerType.Post, true, true) => await Processor.PostAsync(at, request),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            return (id, request.Id);
        }

        public async Task Cancel(string id, Type schedulerType)
        {
            if (schedulerType == typeof(IAmARequestSchedulerSync))
                Provider.GetRequiredService<IAmARequestSchedulerSync>().Cancel(id);
            else if (schedulerType == typeof(IAmARequestSchedulerAsync))
                await Provider.GetRequiredService<IAmARequestSchedulerAsync>().CancelAsync(id);
            else if (schedulerType == typeof(IAmAMessageSchedulerSync))
                Provider.GetRequiredService<IAmAMessageSchedulerSync>().Cancel(id);
            else if (schedulerType == typeof(IAmAMessageSchedulerAsync))
                await Provider.GetRequiredService<IAmAMessageSchedulerAsync>().CancelAsync(id);
            else
                throw new ArgumentOutOfRangeException(nameof(schedulerType));
        }

        public async Task<bool> Reschedule(string id, Type schedulerType, bool useDateTime, TimeSpan delay)
        {
            var at = Clock.GetUtcNow().Add(delay);
            if (schedulerType == typeof(IAmARequestSchedulerSync))
            {
                var scheduler = Provider.GetRequiredService<IAmARequestSchedulerSync>();
                return useDateTime ? scheduler.ReScheduler(id, at) : scheduler.ReScheduler(id, delay);
            }
            if (schedulerType == typeof(IAmARequestSchedulerAsync))
            {
                var scheduler = Provider.GetRequiredService<IAmARequestSchedulerAsync>();
                return useDateTime ? await scheduler.ReSchedulerAsync(id, at) : await scheduler.ReSchedulerAsync(id, delay);
            }
            if (schedulerType == typeof(IAmAMessageSchedulerSync))
            {
                var scheduler = Provider.GetRequiredService<IAmAMessageSchedulerSync>();
                return useDateTime ? scheduler.ReScheduler(id, at) : scheduler.ReScheduler(id, delay);
            }
            if (schedulerType == typeof(IAmAMessageSchedulerAsync))
            {
                var scheduler = Provider.GetRequiredService<IAmAMessageSchedulerAsync>();
                return useDateTime ? await scheduler.ReSchedulerAsync(id, at) : await scheduler.ReSchedulerAsync(id, delay);
            }
            throw new ArgumentOutOfRangeException(nameof(schedulerType));
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }
}
