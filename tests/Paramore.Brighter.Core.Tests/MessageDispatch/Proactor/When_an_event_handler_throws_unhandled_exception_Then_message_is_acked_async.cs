using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpEventProcessingExceptionTestsAsync
    {
        private const string Topic = "MyEvent";
        private const string Channel = "MyChannel";
        private readonly IAmAMessagePump _messagePump;
        private readonly ChannelAsync _channel;
        private readonly int _requeueCount = 5;
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly InternalBus _bus;
        private readonly MessageMapperRegistry _messageMapperRegistry;
        public MessagePumpEventProcessingExceptionTestsAsync()
        {
            SpyExceptionCommandProcessor commandProcessor = new();
            _bus = new InternalBus();
            _channel = new ChannelAsync(new(Channel), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));
            _messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            _messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            _messagePump = new ServiceActivator.Proactor(commandProcessor, (message) => typeof(MyEvent), _messageMapperRegistry, null, new InMemoryRequestContextFactory(), _channel)
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
            _bus.Enqueue(msg);
        }

        [Test]
        public async Task When_an_event_handler_throws_unhandled_exception_Then_message_is_acked_async()
        {
            using (TestCorrelator.CreateContext())
            {
                var task = Task.Factory.StartNew(() => _messagePump.Run(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                await Task.Delay(1000);
                _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
                var quitMessage = new Message(new MessageHeader(string.Empty, RoutingKey.Empty, MessageType.MT_QUIT), new MessageBody(""));
                _channel.Enqueue(quitMessage);
                await Task.WhenAll(task);
                var logEvents = TestCorrelator.GetLogEventsFromCurrentContext();
                await Assert.That((logEvents).Any(x => x.Level == LogEventLevel.Error)).IsTrue();
                await Assert.That(logEvents.First(x => x.Level == LogEventLevel.Error).MessageTemplate.Text).IsEqualTo("MessagePump: Failed to dispatch message {Id} from {ChannelName} with {RoutingKey} on thread # {ManagementThreadId}");
            }
        }
    }
}
