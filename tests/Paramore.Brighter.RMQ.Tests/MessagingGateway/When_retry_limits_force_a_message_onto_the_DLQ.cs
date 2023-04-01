using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Brighter.RMQ.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway
{
    [Trait("Category", "RMQ")]
    [Trait("Fragile", "CI")]
    public class RMQMessageConsumerRetryDLQTests : IDisposable
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly Message _message;
        private readonly ChannelFactory _channelFactory;
        private readonly IAmAChannel _channel;
        private readonly RmqMessageProducer _sender;
        private readonly string _topicName;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly RmqMessageConsumer _deadLetterConsumer;


        public RMQMessageConsumerRetryDLQTests()
        {
            Guid correlationId = Guid.NewGuid();
            string contentType = "text\\plain";
            var channelName = $"Requeue-Limit-Tests-{Guid.NewGuid().ToString()}";
            _topicName = $"Requeue-Limit-Tests-{Guid.NewGuid().ToString()}";
            var routingKey = new RoutingKey(_topicName);

            //what do we send
            var myCommand = new MyDeferredCommand { Value = "Hello Requeue" };
            _message = new Message(
                new MessageHeader(myCommand.Id, _topicName, MessageType.MT_COMMAND, correlationId, "", contentType),
                new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
            );

            var deadLetterQueueName = $"{_message.Header.Topic}.DLQ";
            var deadLetterRoutingKey = $"{_message.Header.Topic}.DLQ";

            var subscription = new RmqSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                //after 2 retries, fail and move to the DLQ
                requeueCount: 2,
                //delay before re-queuing
                requeueDelayInMilliseconds: 50,
                deadLetterChannelName: new ChannelName(deadLetterQueueName),
                deadLetterRoutingKey: deadLetterRoutingKey,
                makeChannels: OnMissingChannel.Create
            );

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange"),
                DeadLetterExchange = new Exchange("paramore.brighter.exchange.dlq")
            };

            //how do we send to the queue
            _sender = new RmqMessageProducer(rmqConnection, new RmqPublication());

            //set up our receiver
            _channelFactory = new ChannelFactory(new RmqMessageConsumerFactory(rmqConnection));
            _channel = _channelFactory.CreateChannel(subscription);

            //how do we handle a command
            IHandleRequests<MyDeferredCommand> handler = new MyDeferredCommandHandler();

            //hook up routing for the command processor
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyDeferredCommand, MyDeferredCommandHandler>();

            //once we read, how do we dispatch to a handler. N.B. we don't use this for reading here
            _commandProcessor = new CommandProcessor(
                subscriberRegistry: subscriberRegistry,
                handlerFactory: new QuickHandlerFactory(() => handler),
                requestContextFactory: new InMemoryRequestContextFactory(),
                policyRegistry: new PolicyRegistry()
            );

            //pump messages from a channel to a handler - in essence we are building our own dispatcher in this test
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyDeferredCommandMessageMapper(_topicName)));
            messageMapperRegistry.Register<MyDeferredCommand, MyDeferredCommandMessageMapper>();

            _messagePump = new MessagePumpBlocking<MyDeferredCommand>(_commandProcessor, messageMapperRegistry)
            {
                Channel = _channel, TimeoutInMilliseconds = 5000, RequeueCount = 3
            };

            _deadLetterConsumer = new RmqMessageConsumer(
                connection: rmqConnection,
                queueName: deadLetterQueueName,
                routingKey: deadLetterRoutingKey,
                isDurable: false,
                makeChannels: OnMissingChannel.Assume
            );
        }

        [Fact]
        public void When_retry_limits_force_a_message_onto_the_dlq()
        {
            //NOTE: This test is **slow** because it needs to ensure infrastructure and then wait whilst we requeue a message a number of times,
            //then propagate to the DLQ

            //start a message pump, let it create infrastructure
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(20000).Wait();

            //put something on an SNS topic, which will be delivered to our SQS queue
            _sender.Send(_message);

            //Let the message be handled and deferred until it reaches the DLQ
            Task.Delay(20000).Wait();

            //send a quit message to the pump to terminate it
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            _channel.Enqueue(quitMessage);

            //wait for the pump to stop once it gets a quit message
            Task.WaitAll(new[] { task });

            Task.Delay(5000).Wait();

            //inspect the dlq
            var dlqMessage = _deadLetterConsumer.Receive(10000).First();

            //assert this is our message
            dlqMessage.Body.Value.Should().Be(_message.Body.Value);

            _deadLetterConsumer.Acknowledge(dlqMessage);
        }

        public void Dispose()
        {
            _channel.Dispose();
        }
    }
}
