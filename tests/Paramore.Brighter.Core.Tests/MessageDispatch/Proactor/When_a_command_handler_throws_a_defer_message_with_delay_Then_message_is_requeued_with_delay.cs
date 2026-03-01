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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpCommandProcessingDeferMessageActionWithDelayTestsAsync
    {
        private const string Topic = "MyCommand";
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new(Topic);

        [Fact]
        public async Task When_a_command_handler_throws_a_defer_message_with_delay_Then_message_is_requeued_with_that_delay()
        {
            //Arrange
            var bus = new InternalBus();
            var timeProvider = new FakeTimeProvider();
            var consumer = new InMemoryMessageConsumer(_routingKey, bus, timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000));
            var spyChannel = new SpyChannelAsync(new ChannelName(ChannelName), _routingKey, consumer);

            var commandProcessor = new SpyRequeueWithDelayCommandProcessor(delayMilliseconds: 5000);

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

            var messagePump = new ServiceActivator.Proactor(
                commandProcessor,
                (message) => typeof(MyCommand),
                messageMapperRegistry,
                null,
                new InMemoryRequestContextFactory(),
                spyChannel)
            {
                Channel = spyChannel,
                TimeOut = TimeSpan.FromMilliseconds(5000),
                RequeueCount = 5,
                RequeueDelay = TimeSpan.FromMilliseconds(100) // Subscription default — should NOT be used when DeferMessageAction has a delay
            };

            var msg = new TransformPipelineBuilderAsync(messageMapperRegistry, null, InstrumentationOptions.All)
                .BuildWrapPipeline<MyCommand>()
                .WrapAsync(new MyCommand(), new RequestContext(), new Publication { Topic = _routingKey })
                .Result;
            bus.Enqueue(msg);

            //Act
            var task = Task.Factory.StartNew(() => messagePump.Run(), TaskCreationOptions.LongRunning);
            await Task.Delay(1000);

            timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages

            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            spyChannel.Enqueue(quitMessage);

            await Task.WhenAll(task);

            //Assert
            Assert.NotEmpty(spyChannel.RequeueDelays); // At least one requeue occurred
            Assert.Equal(TimeSpan.FromMilliseconds(5000), spyChannel.RequeueDelays[0]); // Delay from DeferMessageAction, not subscription RequeueDelay (100ms)
        }

        /// <summary>
        /// Spy channel that captures the delay passed to RequeueAsync for test verification.
        /// </summary>
        private class SpyChannelAsync : Brighter.ChannelAsync
        {
            public List<TimeSpan?> RequeueDelays { get; } = [];

            public SpyChannelAsync(ChannelName channelName, RoutingKey routingKey, IAmAMessageConsumerAsync consumer)
                : base(channelName, routingKey, consumer) { }

            public override Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
            {
                RequeueDelays.Add(timeOut);
                // Pass zero delay to base so InMemoryMessageConsumer does a simple no-delay requeue
                return base.RequeueAsync(message, TimeSpan.Zero, cancellationToken);
            }
        }
    }
}
