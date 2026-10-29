#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class DispatcherConsumerDisposalFailureTests
{
    [Theory]
    [InlineData(MessagePumpType.Reactor, typeof(InvalidOperationException), 1, true)]
    [InlineData(MessagePumpType.Reactor, typeof(ObjectDisposedException), 1, true)]
    [InlineData(MessagePumpType.Reactor, typeof(AggregateException), 1, true)]
    [InlineData(MessagePumpType.Reactor, typeof(TaskCanceledException), 1, true)]
    [InlineData(MessagePumpType.Reactor, typeof(InvalidOperationException), 2, true)]
    [InlineData(MessagePumpType.Proactor, typeof(InvalidOperationException), 1, true)]
    [InlineData(MessagePumpType.Proactor, typeof(ObjectDisposedException), 1, true)]
    [InlineData(MessagePumpType.Proactor, typeof(AggregateException), 1, true)]
    [InlineData(MessagePumpType.Proactor, typeof(TaskCanceledException), 1, true)]
    [InlineData(MessagePumpType.Proactor, typeof(InvalidOperationException), 2, true)]
    [InlineData(MessagePumpType.Reactor, typeof(InvalidOperationException), 1, false)]
    [InlineData(MessagePumpType.Reactor, typeof(ObjectDisposedException), 1, false)]
    [InlineData(MessagePumpType.Reactor, typeof(AggregateException), 1, false)]
    [InlineData(MessagePumpType.Reactor, typeof(TaskCanceledException), 1, false)]
    [InlineData(MessagePumpType.Proactor, typeof(InvalidOperationException), 1, false)]
    [InlineData(MessagePumpType.Proactor, typeof(ObjectDisposedException), 1, false)]
    [InlineData(MessagePumpType.Proactor, typeof(AggregateException), 1, false)]
    [InlineData(MessagePumpType.Proactor, typeof(TaskCanceledException), 1, false)]
    public async Task When_consumer_disposal_throws_should_drain_remaining_consumers_and_allow_restart(
        MessagePumpType pumpType, Type exceptionType, int failingConsumerCount, bool failAcknowledgment)
    {
        //Arrange
        using var logs = TestCorrelator.CreateContext();
        var timeout = TimeSpan.FromSeconds(10);
        Exception failure = exceptionType == typeof(AggregateException)
            ? new AggregateException(new InvalidOperationException("Consumer disposal failed"))
            : (Exception)Activator.CreateInstance(exceptionType, "Consumer disposal failed")!;
        var factory = new InMemoryDisposalFailureChannelFactory(failure, failingConsumerCount, failAcknowledgment);
        var subscriptions = Enumerable.Range(0, failingConsumerCount + 1)
            .Select(index => CreateSubscription($"consumer-{index}", pumpType, factory)).ToArray();
        using var mappers = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        mappers.Register<MyEvent, MyEventMessageMapper>();
        mappers.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        using var dispatcher = new Dispatcher(new SpyCommandProcessor(), subscriptions, mappers, mappers);
        dispatcher.Receive();
        var jobs = dispatcher.Consumers.Select(consumer => consumer.Job!).ToList();

        try
        {
            //Act
            foreach (var subscription in subscriptions.Take(failingConsumerCount))
            {
                if (failAcknowledgment)
                {
                    factory.Publish(new MyEventMessageMapper().MapToMessage(
                        new MyEvent(), new Publication { Topic = subscription.RoutingKey }));
                }
                else
                {
                    dispatcher.Shut(subscription);
                }
            }

            await Task.WhenAll(factory.Consumers.Take(failingConsumerCount).Select(consumer => consumer.DisposalAttempted))
                .WaitAsync(timeout);
            var exception = await Record.ExceptionAsync(async () => await dispatcher.End().WaitAsync(timeout));

            //Assert
            Assert.Null(exception);
            Assert.Equal(DispatcherState.DS_STOPPED, dispatcher.State);
            Assert.Empty(dispatcher.Consumers);
            Assert.Equal(subscriptions.Length, factory.Consumers.Count);
            Assert.All(factory.Consumers, consumer => Assert.Equal(1, consumer.DisposeCount));
            Assert.All(jobs, job => Assert.Throws<ObjectDisposedException>(() => ((IAsyncResult)job).AsyncWaitHandle));
            var loggedFailure = failure is AggregateException aggregate ? aggregate.InnerException : failure;
            Assert.Equal(failingConsumerCount, TestCorrelator.GetLogEventsFromCurrentContext().Count(entry =>
                entry.Level == LogEventLevel.Error && ReferenceEquals(entry.Exception, loggedFailure)));

            //Act
            dispatcher.Receive();
            jobs.AddRange(dispatcher.Consumers.Select(consumer => consumer.Job!));

            //Assert
            Assert.Equal(DispatcherState.DS_RUNNING, dispatcher.State);
            Assert.Equal(subscriptions.Length, dispatcher.Consumers.Count());
            await dispatcher.End().WaitAsync(timeout);
            Assert.Equal(DispatcherState.DS_STOPPED, dispatcher.State);
            Assert.Empty(dispatcher.Consumers);
            Assert.Equal(subscriptions.Length * 2, factory.Consumers.Count);
            Assert.All(factory.Consumers, consumer => Assert.Equal(1, consumer.DisposeCount));
            Assert.All(jobs, job => Assert.Throws<ObjectDisposedException>(() => ((IAsyncResult)job).AsyncWaitHandle));
        }
        finally
        {
            await Record.ExceptionAsync(async () => await dispatcher.End().WaitAsync(timeout));
            await Record.ExceptionAsync(async () => await Task.WhenAll(jobs).WaitAsync(timeout));
            foreach (var consumer in factory.Consumers)
                consumer.ReleaseResources();
        }
    }

    private static Subscription CreateSubscription(string name, MessagePumpType pumpType, IAmAChannelFactory factory)
        => new Subscription<MyEvent>(new SubscriptionName(name),
            channelName: new ChannelName(name), routingKey: new RoutingKey(name),
            channelFactory: factory, messagePumpType: pumpType, noOfPerformers: 1,
            timeOut: TimeSpan.FromMilliseconds(10), emptyChannelDelay: TimeSpan.FromMilliseconds(10));
}
