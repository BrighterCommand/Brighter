using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpCommandProcessingExceptionTestsAsync
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly ChannelAsync _channel;
        private readonly int _requeueCount = 5;
        private readonly RoutingKey _routingKey = new("MyCommand");
        private readonly FakeTimeProvider _timeProvider = new();

        public MessagePumpCommandProcessingExceptionTestsAsync()
        {
            SpyExceptionCommandProcessor commandProcessor = new();

            InternalBus bus = new();
            
            _channel = new ChannelAsync(new("myChannel"),_routingKey, new InMemoryMessageConsumer(_routingKey, bus, _timeProvider, TimeSpan.FromMilliseconds(1000)));
            
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
             
            _messagePump = new ServiceActivator.Proactor(commandProcessor, (message) => typeof(MyCommand), 
                messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), _channel
                )
            {
                Channel = _channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = _requeueCount
            };

            var msg = new TransformPipelineBuilderAsync(messageMapperRegistry, null, InstrumentationOptions.All)
                .BuildWrapPipeline<MyCommand>()
                .WrapAsync(new MyCommand(),  new RequestContext(), new Publication{Topic = _routingKey})
                .Result;
            bus.Enqueue(msg);
            
        }

        [Fact]
        public async Task When_a_command_handler_throws_unhandled_exception_Then_message_is_acked_async()
        {
            using (TestCorrelator.CreateContext())
            {
                var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
                await Task.Delay(1000);

                _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
                
                var quitMessage = new Message(new MessageHeader(string.Empty, RoutingKey.Empty, MessageType.MT_QUIT),
                    new MessageBody(""));
                _channel.Enqueue(quitMessage);

                await Task.WhenAll(task);

                var logEvents = TestCorrelator.GetLogEventsFromCurrentContext();
                Assert.Contains(logEvents, x => x.Level == LogEventLevel.Error);
                Assert.Equal(
                    "MessagePump: Failed to dispatch message '{Id}' from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}",
                    logEvents.First(x => x.Level == LogEventLevel.Error).MessageTemplate.Text);
            }
        }
    }
}
