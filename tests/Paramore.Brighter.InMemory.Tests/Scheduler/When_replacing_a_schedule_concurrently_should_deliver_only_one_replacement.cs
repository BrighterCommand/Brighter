#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Scheduler;

[Trait("Category", "InMemory")]
[Collection(ThreadPoolSaturatingCollection.Name)]
public class ConcurrentSchedulerReplacementTests : IDisposable
{
    private static readonly AsyncLocal<string> s_callbackContext = new();
    private string _observedCallbackContext = string.Empty;
    private readonly InMemorySchedulerFactory _schedulerFactory;
    private readonly IAmACommandProcessor _processor;
    private readonly InternalBus _internalBus = new();
    private readonly RoutingKey _routingKey;
    private readonly CoordinatedSchedulerTimeProvider _timeProvider;
    private const string FIXED_SCHEDULER_ID = "fixed-scheduler-id";

    public ConcurrentSchedulerReplacementTests()
    {
        _routingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
        _timeProvider = new CoordinatedSchedulerTimeProvider();

        _schedulerFactory = new InMemorySchedulerFactory
        {
            TimeProvider = _timeProvider,
            GetOrCreateMessageSchedulerId = _ => FIXED_SCHEDULER_ID,
            GetOrCreateRequestSchedulerId = _ => FIXED_SCHEDULER_ID,
            OnConflict = OnSchedulerConflict.Overwrite
        };

        var handlerFactory = new SimpleHandlerFactory(
            _ => new MyEventHandler(new Dictionary<string, string>()),
            type => type == typeof(FireSchedulerMessageHandler)
                ? new FireSchedulerMessageHandler(_processor!)
                : new FireSchedulerRequestHandler(_processor!));

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyEvent, MyEventHandler>();
        subscriberRegistry.RegisterAsync<FireSchedulerMessage, FireSchedulerMessageHandler>();
        subscriberRegistry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();

        var policyRegistry = new PolicyRegistry
        {
            [CommandProcessor.RETRYPOLICY] = Policy.Handle<Exception>().Retry(),
            [CommandProcessor.CIRCUITBREAKER] =
                Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(1))
        };

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            [_routingKey] = new InMemoryMessageProducer(_internalBus, new Publication { Topic = _routingKey, RequestType = typeof(MyEvent) })
            {
                PublishFailurePredicate = _ =>
                {
                    _observedCallbackContext = s_callbackContext.Value;
                    return false;
                }
            }
        });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));

        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

        var trace = new BrighterTracer(_timeProvider);
        var outbox = new InMemoryOutbox(_timeProvider)
        {
            Tracer = trace,
            EntryTimeToLive = TimeSpan.FromHours(3)
        };

        var outboxBus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            trace,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox
        );

        _processor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            outboxBus,
            _schedulerFactory
        );
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_replacing_a_schedule_concurrently_should_deliver_only_one_replacement(
        bool asyncSchedule, bool requestSchedule)
    {
        //Arrange
        using var scheduler = (InMemoryScheduler)_schedulerFactory.Create(_processor);
        scheduler.Schedule(CreateMessage(0), TimeSpan.FromMinutes(1));
        _timeProvider.CoordinateNextTwoTimers();

        //Act
        await ReplaceConcurrently(scheduler, asyncSchedule, requestSchedule);
        Assert.Equal(1, _timeProvider.ActiveTimerCount);
        _timeProvider.Advance(TimeSpan.FromMinutes(2));

        //Assert
        var delivered = Assert.Single(_internalBus.Stream(_routingKey));
        Assert.Contains(delivered.Id.Value, new[] { "event-1", "event-2" });
        Assert.False(scheduler.ReScheduler(FIXED_SCHEDULER_ID, TimeSpan.FromMinutes(1)));
        Assert.Equal(0, _timeProvider.ActiveTimerCount);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task When_cancelling_concurrent_replacements_should_deliver_no_messages(
        bool asyncSchedule, bool requestSchedule, bool asyncCancel)
    {
        //Arrange
        using var scheduler = (InMemoryScheduler)_schedulerFactory.Create(_processor);
        scheduler.Schedule(CreateMessage(0), TimeSpan.FromMinutes(1));
        _timeProvider.CoordinateNextTwoTimers();
        await ReplaceConcurrently(scheduler, asyncSchedule, requestSchedule);

        //Act
        if (asyncCancel)
        {
            await scheduler.CancelAsync(FIXED_SCHEDULER_ID);
        }
        else
        {
            scheduler.Cancel(FIXED_SCHEDULER_ID);
        }
        Assert.Equal(0, _timeProvider.ActiveTimerCount);
        _timeProvider.Advance(TimeSpan.FromMinutes(2));

        //Assert
        Assert.Empty(_internalBus.Stream(_routingKey));
        Assert.False(scheduler.ReScheduler(FIXED_SCHEDULER_ID, TimeSpan.FromMinutes(1)));
        Assert.Equal(0, _timeProvider.ActiveTimerCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_scheduling_with_zero_delay_should_deliver_once_and_release_the_id(
        bool asyncSchedule, bool requestSchedule)
    {
        //Arrange
        using var scheduler = (InMemoryScheduler)_schedulerFactory.Create(_processor);

        //Act
        await Schedule(scheduler, 1, TimeSpan.Zero, asyncSchedule, requestSchedule);

        //Assert
        Assert.Equal("event-1", Assert.Single(_internalBus.Stream(_routingKey)).Id.Value);
        Assert.False(scheduler.ReScheduler(FIXED_SCHEDULER_ID, TimeSpan.FromMinutes(1)));
        Assert.Equal(0, _timeProvider.ActiveTimerCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_rejecting_a_duplicate_with_zero_delay_should_not_dispatch_it(
        bool asyncSchedule, bool requestSchedule)
    {
        //Arrange
        _schedulerFactory.OnConflict = OnSchedulerConflict.Throw;
        using var scheduler = (InMemoryScheduler)_schedulerFactory.Create(_processor);
        scheduler.Schedule(CreateMessage(0), TimeSpan.FromMinutes(1));

        //Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Schedule(scheduler, 1, TimeSpan.Zero, asyncSchedule, requestSchedule));

        //Assert
        Assert.Empty(_internalBus.Stream(_routingKey));
        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("event-0", Assert.Single(_internalBus.Stream(_routingKey)).Id.Value);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_rescheduling_during_activation_should_keep_the_rescheduled_delay(
        bool asyncSchedule, bool requestSchedule)
    {
        //Arrange
        using var scheduler = (InMemoryScheduler)_schedulerFactory.Create(_processor);
        _timeProvider.PauseNextActivation();
        var scheduling = Task.Factory.StartNew(
            () => Schedule(scheduler, 1, TimeSpan.FromMinutes(10), asyncSchedule, requestSchedule),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

        //Act
        Task<bool> rescheduling = Task.FromResult(false);
        try
        {
            Assert.True(_timeProvider.WaitForActivation());
            rescheduling = Task.Factory.StartNew(
                () => scheduler.ReSchedulerAsync(FIXED_SCHEDULER_ID, TimeSpan.FromMinutes(1)),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
            await Task.WhenAny(rescheduling, Task.Delay(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            _timeProvider.ResumeActivation();
            await scheduling.WaitAsync(TimeSpan.FromSeconds(30));
        }
        Assert.True(await rescheduling.WaitAsync(TimeSpan.FromSeconds(30)));
        _timeProvider.Advance(TimeSpan.FromMinutes(2));

        //Assert
        Assert.Equal("event-1", Assert.Single(_internalBus.Stream(_routingKey)).Id.Value);
        Assert.Equal(0, _timeProvider.ActiveTimerCount);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        Assert.Single(_internalBus.Stream(_routingKey));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_a_timer_fires_on_another_thread_during_activation_should_preserve_callback_context(
        bool asyncSchedule, bool requestSchedule)
    {
        //Arrange
        using var scheduler = (InMemoryScheduler)_schedulerFactory.Create(_processor);
        s_callbackContext.Value = "scheduling-context";
        _timeProvider.CallbackOnWorker = true;
        _timeProvider.BeforeCallback = () => s_callbackContext.Value = "timer-callback-context";

        //Act
        await Schedule(scheduler, 1, TimeSpan.Zero, asyncSchedule, requestSchedule);

        //Assert
        Assert.Equal("event-1", Assert.Single(_internalBus.Stream(_routingKey)).Id.Value);
        Assert.Equal("timer-callback-context", _observedCallbackContext);
        Assert.Equal("scheduling-context", s_callbackContext.Value);
        Assert.Equal(0, _timeProvider.ActiveTimerCount);
    }

    private Task<string> Schedule(InMemoryScheduler scheduler, int index, TimeSpan delay,
        bool asyncSchedule, bool requestSchedule)
    {
        if (requestSchedule)
        {
            var request = new MyEvent { Id = $"event-{index}" };
            return asyncSchedule
                ? scheduler.ScheduleAsync(request, RequestSchedulerType.Post, delay)
                : Task.FromResult(scheduler.Schedule(request, RequestSchedulerType.Post, delay));
        }

        return asyncSchedule
            ? scheduler.ScheduleAsync(CreateMessage(index), delay)
            : Task.FromResult(scheduler.Schedule(CreateMessage(index), delay));
    }

    private async Task ReplaceConcurrently(InMemoryScheduler scheduler, bool asyncSchedule, bool requestSchedule)
    {
        var first = StartReplacement(1);
        var second = StartReplacement(2);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30));

        Task StartReplacement(int index) => Task.Factory.StartNew(
            () => Schedule(scheduler, index, TimeSpan.FromMinutes(1), asyncSchedule, requestSchedule),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    private Message CreateMessage(int index)
    {
        var evt = new MyEvent { Id = $"event-{index}" };
        return new Message(
            new MessageHeader
            {
                MessageId = evt.Id,
                MessageType = MessageType.MT_EVENT,
                Topic = _routingKey
            },
            new MessageBody(JsonSerializer.Serialize(evt)));
    }

    public void Dispose() => _timeProvider.Dispose();
}
