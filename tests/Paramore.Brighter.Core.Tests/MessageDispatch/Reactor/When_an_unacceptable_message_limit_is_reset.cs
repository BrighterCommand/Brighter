using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpUnacceptableMessageLimitResetTests
    {
        private const string Channel = "MyChannel";
        private readonly IAmAMessagePump _messagePump;
        private readonly InternalBus _bus;
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly Channel _channel;
        private readonly Message _unacceptableMessage1;
        private readonly Message _unacceptableMessage2;
        private readonly Message _unacceptableMessage3;
        private readonly Message _unacceptableMessage4;
        private readonly Message _timeAdvanceMessage;

        public MessagePumpUnacceptableMessageLimitResetTests()
        {
            _bus = new InternalBus();
            
            _channel = new Channel(
                new(Channel),
                _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)), 
                10
                );
            
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyAdvanceTimerEvent, MyAdvanceTimerEventHandler>();

            var handlerFactory = new SimpleHandlerFactory(
                (type) => new MyAdvanceTimerEventHandler(_timeProvider),
                (type) => throw new NotImplementedException()
            );

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>();
            resiliencePipelineRegistry.AddBrighterDefault();            
            
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyAdvanceTimerEventMessageMapper()),
                new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException()));
            messageMapperRegistry.Register<MyAdvanceTimerEvent, MyAdvanceTimerEventMessageMapper>();

            var commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                resiliencePipelineRegistry,
                new InMemorySchedulerFactory()
            );
            
            _messagePump = new ServiceActivator.Reactor(commandProcessor, (message) => typeof(MyAdvanceTimerEvent), 
                messageMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), _channel, 
                timeProvider:_timeProvider)
            {
                Channel = _channel, 
                TimeOut = TimeSpan.FromMilliseconds(5000), 
                RequeueCount = 3, 
                UnacceptableMessageLimit = 3, 
                UnacceptableMessageLimitWindow = TimeSpan.FromMinutes(1)
            };

            _unacceptableMessage1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );
            _unacceptableMessage2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );
            _unacceptableMessage3 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );
            _unacceptableMessage4 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );

            _timeAdvanceMessage = new MyAdvanceTimerEventMessageMapper().MapToMessage(
                new MyAdvanceTimerEvent(2), 
                new Publication<MyAdvanceTimerEvent>
                {
                    Topic = _routingKey
                });

        }

        [Fact]
        public async Task When_An_Unacceptable_Message_Limit_Is_Reached()
        {
            _channel.Enqueue(_unacceptableMessage1);
            _channel.Enqueue(_unacceptableMessage2);
            
            //force the time forward, whilst in the message loop
            _channel.Enqueue(_timeAdvanceMessage);
            

            //will trigger reset of unacceptable message count as window has passed
            _channel.Enqueue(_unacceptableMessage3);
            _channel.Enqueue(_unacceptableMessage4);

            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
            
           
            _channel.Stop(_routingKey);
            
            await Task.WhenAll(task);
            
            Assert.Empty(_bus.Stream(_routingKey));

            Assert.Equal(MessagePumpStatus.MP_STOPPED, _messagePump.Status);
        }
    }
}

