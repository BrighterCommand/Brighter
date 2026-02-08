#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Scheduler;

/// <summary>
/// Tests that the InMemoryScheduler uses atomic operations when replacing timers
/// for messages with the same scheduler ID, preventing race conditions.
/// </summary>
[Trait("Category", "InMemory")]
[Collection("CommandProcess")]
public class When_scheduling_message_with_existing_id_should_atomically_replace_timer
{
    private readonly InMemorySchedulerFactory _schedulerFactory;
    private readonly IAmACommandProcessor _processor;
    private readonly InternalBus _internalBus = new();
    private readonly RoutingKey _routingKey;
    private readonly FakeTimeProvider _timeProvider;
    private const string FixedSchedulerId = "fixed-scheduler-id";

    public When_scheduling_message_with_existing_id_should_atomically_replace_timer()
    {
        _routingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        // Configure scheduler to use a fixed ID so multiple Schedule calls target the same entry
        // and to overwrite (not throw) on conflict
        _schedulerFactory = new InMemorySchedulerFactory
        {
            TimeProvider = _timeProvider,
            GetOrCreateMessageSchedulerId = _ => FixedSchedulerId,
            OnConflict = OnSchedulerConflict.Overwrite
        };

        var handlerFactory = new SimpleHandlerFactory(
            _ => new MyEventHandler(new Dictionary<string, string>()),
            _ => new FireSchedulerMessageHandler(_processor!));

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyEvent, MyEventHandler>();
        subscriberRegistry.RegisterAsync<FireSchedulerMessage, FireSchedulerMessageHandler>();

        var policyRegistry = new PolicyRegistry
        {
            [CommandProcessor.RETRYPOLICY] = Policy.Handle<Exception>().Retry(),
            [CommandProcessor.CIRCUITBREAKER] =
                Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(1))
        };

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            [_routingKey] = new InMemoryMessageProducer(_internalBus, new Publication { Topic = _routingKey, RequestType = typeof(MyEvent) })
        });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            null);

        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

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

        CommandProcessor.ClearServiceBus();
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

    [Fact]
    public async Task When_scheduling_same_id_concurrently_should_not_have_race_condition()
    {
        // Arrange
        const int concurrentScheduleCalls = 100;
        var scheduler = (IAmAMessageSchedulerSync)_schedulerFactory.Create(_processor);
        var barrier = new Barrier(concurrentScheduleCalls);
        var exceptions = new List<Exception>();
        var tasks = new Task[concurrentScheduleCalls];

        // Act - Schedule many messages concurrently with the same ID
        for (int i = 0; i < concurrentScheduleCalls; i++)
        {
            var messageIndex = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    var message = CreateMessage(messageIndex);
                    barrier.SignalAndWait(); // Synchronize all threads to start at the same time
                    scheduler.Schedule(message, TimeSpan.FromMinutes(1));
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert - No exceptions should occur during concurrent scheduling
        Assert.Empty(exceptions);
    }

    [Fact]
    public void When_replacing_timer_should_dispose_old_timer_before_creating_new()
    {
        // Arrange
        var scheduler = (IAmAMessageSchedulerSync)_schedulerFactory.Create(_processor);
        var firstMessage = CreateMessage(1);
        var secondMessage = CreateMessage(2);

        // Act - Schedule first message
        var firstId = scheduler.Schedule(firstMessage, TimeSpan.FromMinutes(10));

        // Schedule second message with same ID (should replace)
        var secondId = scheduler.Schedule(secondMessage, TimeSpan.FromMinutes(5));

        // Assert - Both calls return the same scheduler ID
        Assert.Equal(FixedSchedulerId, firstId);
        Assert.Equal(FixedSchedulerId, secondId);

        // Advance time past the second delay (5 minutes) but not the first (10 minutes)
        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        // The second message should have been delivered (timer was replaced with shorter delay)
        // Only one message should be in the bus (no orphaned timer delivering the first message)
        var messages = _internalBus.Stream(_routingKey);
        Assert.Single(messages);
    }

    [Fact]
    public void When_scheduling_same_id_multiple_times_should_only_deliver_last_message()
    {
        // Arrange
        var scheduler = (IAmAMessageSchedulerSync)_schedulerFactory.Create(_processor);
        const int scheduleCount = 10;

        // Act - Schedule multiple messages with the same ID
        for (int i = 0; i < scheduleCount; i++)
        {
            var message = CreateMessage(i);
            scheduler.Schedule(message, TimeSpan.FromMinutes(1));
        }

        // Advance time to trigger delivery
        _timeProvider.Advance(TimeSpan.FromMinutes(2));

        // Assert - Only ONE message should be delivered (no orphaned timers)
        var deliveredMessages = _internalBus.Stream(_routingKey);
        Assert.Single(deliveredMessages);
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
}
