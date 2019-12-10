using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class MessageDispatcherRoutingAsyncTests
    {
        private readonly Dispatcher _dispatcher;
        private readonly FakeChannel _channel;
        private readonly SpyCommandProcessor _commandProcessor;

        public MessageDispatcherRoutingAsyncTests()
        {
            _channel = new FakeChannel();
            _commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var connection = new Connection<MyEvent>(
                new ConnectionName("test"),
                noOfPerformers: 1, 
                timeoutInMilliseconds: 1000, 
                channelFactory: new InMemoryChannelFactory(_channel),
                channelName: new ChannelName("fakeChannel"), 
                routingKey: new RoutingKey("fakekey"),
                isAsync: true);
            _dispatcher = new Dispatcher(_commandProcessor, messageMapperRegistry, new List<Connection> { connection });

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event);
            _channel.Enqueue(message);

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }
   }
}
