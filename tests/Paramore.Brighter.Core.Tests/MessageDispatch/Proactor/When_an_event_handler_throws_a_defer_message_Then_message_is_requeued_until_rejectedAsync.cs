#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpEventProcessingDeferMessageActionTestsAsync
    {
        private const string Topic = "MyEvent";
        private const string Channel = "MyChannel";
        private readonly IAmAMessagePump _messagePump;
        private readonly int _requeueCount = 5;
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly InternalBus _bus;
        private readonly ChannelAsync _channel;
        private readonly MessageMapperRegistry _messageMapperRegistry;
        public MessagePumpEventProcessingDeferMessageActionTestsAsync()
        {
            SpyRequeueCommandProcessor commandProcessor = new();
            _bus = new InternalBus();
            _channel = new ChannelAsync(new(Channel), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));
            _messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            _messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            _messagePump = new ServiceActivator.Proactor(commandProcessor, (message) => typeof(MyEvent), _messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), _channel)
            {
                Channel = _channel,
                TimeOut = TimeSpan.FromMilliseconds(5000),
                RequeueCount = _requeueCount
            };
        }

        [Before(Test)]
        public async Task Setup()
        {
            var msg = await new TransformPipelineBuilderAsync(_messageMapperRegistry, null, InstrumentationOptions.All).BuildWrapPipeline<MyEvent>().WrapAsync(new MyEvent(), new RequestContext(), new Publication { Topic = _routingKey });
            _channel.Enqueue(msg);
        }

        [Test]
        public async Task When_an_event_handler_throws_a_defer_message_the_message_is_requeued_until_rejectedAsync()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await Task.Delay(1000);
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            var quitMessage = MessageFactory.CreateQuitMessage(new RoutingKey(Topic));
            _channel.Enqueue(quitMessage);
            await Task.WhenAll(task);
            await Assert.That(_bus.Stream(_routingKey)).IsEmpty();
        }
    }
}