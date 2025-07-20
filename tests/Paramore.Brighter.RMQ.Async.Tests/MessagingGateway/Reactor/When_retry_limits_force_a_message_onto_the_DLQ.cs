using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Trait("Fragile", "CI")]
public class RMQMessageConsumerRetryDLQTests : IDisposable
{
    private readonly IAmAMessagePump _messagePump;
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly RmqMessageProducer _sender;
    private readonly RmqMessageConsumer _deadLetterConsumer;
    private readonly RmqSubscription<MyCommand> _subscription;


    public RMQMessageConsumerRetryDLQTests()
    {
        string correlationId = Guid.NewGuid().ToString();
        var contentType = new ContentType(MediaTypeNames.Text.Plain){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()};
        var channelName = new ChannelName($"Requeue-Limit-Tests-{Guid.NewGuid().ToString()}");
        var routingKey = new RoutingKey($"Requeue-Limit-Tests-{Guid.NewGuid().ToString()}");

        //what do we send
        var myCommand = new MyDeferredCommand { Value = "Hello Requeue" };
        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId, 
                contentType: contentType
            ),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var deadLetterQueueName = new ChannelName($"{Guid.NewGuid().ToString()}.DLQ");
        var deadLetterRoutingKey = new RoutingKey( $"{_message.Header.Topic}.DLQ");

        _subscription = new RmqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("DLQ Test Subscription"),
            channelName: channelName,
            routingKey: routingKey,
            //after 0 retries fail and move to the DLQ
            requeueCount: 0,
            //delay before re-queuing
            requeueDelay: TimeSpan.FromMilliseconds(50),
            deadLetterChannelName: deadLetterQueueName,
            deadLetterRoutingKey: deadLetterRoutingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create
        );

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange"),
            DeadLetterExchange = new Exchange("paramore.brighter.exchange.dlq")
        };

        //how do we send to the queue
        _sender = new RmqMessageProducer(rmqConnection, new RmqPublication
        {
            Topic = routingKey, 
            RequestType = typeof(MyDeferredCommand)
        });

        //set up our receiver
        ChannelFactory channelFactory = new(new RmqMessageConsumerFactory(rmqConnection));
        _channel = channelFactory.CreateSyncChannel(_subscription);

        //how do we handle a command
        IHandleRequests<MyDeferredCommand> handler = new MyDeferredCommandHandler();

        //hook up routing for the command processor
        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<MyDeferredCommand, MyDeferredCommandHandler>();

        //once we read, how do we dispatch to a handler. N.B. we don't use this for reading here
        IAmACommandProcessor commandProcessor = new CommandProcessor(
            subscriberRegistry: subscriberRegistry,
            handlerFactory: new QuickHandlerFactory(() => handler),
            requestContextFactory: new InMemoryRequestContextFactory(),
            policyRegistry: new PolicyRegistry(),
            requestSchedulerFactory: new InMemorySchedulerFactory()
        );

        //pump messages from a channel to a handler - in essence we are building our own dispatcher in this test
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyDeferredCommandMessageMapper()),
            null
        );
            
        messageMapperRegistry.Register<MyDeferredCommand, MyDeferredCommandMessageMapper>();
            
        _messagePump = new ServiceActivator.Reactor(commandProcessor, (message) => typeof(MyDeferredCommand), messageMapperRegistry, 
            new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), _channel)
        {
            Channel = _channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = 0
        };

        _deadLetterConsumer = new RmqMessageConsumer(
            connection: rmqConnection,
            queueName: deadLetterQueueName,
            routingKey: deadLetterRoutingKey,
            isDurable: false,
            makeChannels: OnMissingChannel.Assume
        );
    }

    [Fact(Skip = "Breaks due to fault in Task Scheduler running after context has closed")]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method")]
    public async Task When_retry_limits_force_a_message_onto_the_dlq()
    {
        //NOTE: This test is **slow** because it needs to ensure infrastructure and then wait whilst we requeue a message a number of times,
        //then propagate to the DLQ
            
        //start a message pump, let it create infrastructure 
        var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
        await Task.Delay(20000);

        //put something on an SNS topic, which will be delivered to our SQS queue
        _sender.Send(_message);

        //Let the message be handled and deferred until it reaches the DLQ
        await Task.Delay(20000);

        //send a quit message to the pump to terminate it 
        var quitMessage = MessageFactory.CreateQuitMessage(_subscription.RoutingKey);
        _channel.Enqueue(quitMessage);

        //wait for the pump to stop once it gets a quit message
        await Task.WhenAll(task);

        await Task.Delay(5000);

        //inspect the dlq
        var dlqMessage = _deadLetterConsumer.Receive(new TimeSpan(10000)).First();

        //assert this is our message
        Assert.Equal(_message.Body.Value, dlqMessage.Body.Value);

        _deadLetterConsumer.Acknowledge(dlqMessage);

    }

    public void Dispose()
    {
        _channel.Dispose();
        _deadLetterConsumer.Dispose();
    }

}
