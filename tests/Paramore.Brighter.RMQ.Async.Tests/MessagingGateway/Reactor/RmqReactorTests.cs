using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

public class RmqReactorTests :  MessagingGatewayReactorTests<RmqPublication, RmqSubscription>
{
    protected int? MaxQueueLenght { get; set; }
    protected int BufferSize { get; set; } = 2;
    
    private IAmAChannelSync? DeadLetterQueueChannel { get; set; }
    
    protected override bool HasSupportToDeadLetterQueue => true;

    protected virtual bool IsDurable { get; } = false;
    protected virtual TimeSpan? Ttl { get; } = null;
    protected virtual QueueType QueueType => QueueType.Classic;
    
    protected virtual RmqMessagingGatewayConnection CreateConnection()
    {
        return Configuration.CreateConnection();
    }

    protected override void CleanUp()
    {
        if (DeadLetterQueueChannel != null)
        {
            try
            {
                DeadLetterQueueChannel.Purge();
                DeadLetterQueueChannel.Dispose();
                DeadLetterQueueChannel = null;
            }
            catch 
            {
                // Ignoring any error
            }
        }
        
        base.CleanUp();
    }

    protected override RmqPublication CreatePublication(RoutingKey routingKey)
    {
        return new RmqPublication<MyCommand>
        {
            Topic = routingKey,
            MakeChannels = OnMissingChannel.Create,
        };
    }

    protected override RmqSubscription CreateSubscription(RoutingKey routingKey, 
        ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create,
        bool setupDeadLetterQueue = false)
    {
        ChannelName? deadLetterChannelName = null;
        RoutingKey? deadLetterRoutingKey = null;

        if (setupDeadLetterQueue)
        {
            deadLetterChannelName = new ChannelName(channelName.Value + "DLQ");
            deadLetterRoutingKey = new RoutingKey(routingKey.Value + "DLQ");
        }
        
        var subscription = new RmqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            requeueCount: 3,
            maxQueueLength: MaxQueueLenght,
            ttl: Ttl,
            deadLetterChannelName: deadLetterChannelName,
            deadLetterRoutingKey: deadLetterRoutingKey,
            bufferSize: BufferSize,
            queueType: QueueType,
            isDurable: IsDurable);

        if (setupDeadLetterQueue)
        {
            GetMessageFromDeadLetterQueue(subscription);
        }

        return subscription;
    }

    protected override Message GetMessageFromDeadLetterQueue(RmqSubscription subscription)
    {
        if (DeadLetterQueueChannel == null)
        {
            var sub = new RmqSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(Uuid.NewAsString()),
                channelName: subscription.DeadLetterChannelName,
                routingKey: subscription.DeadLetterRoutingKey,
                messagePumpType: MessagePumpType.Proactor,
                queueType: QueueType.Classic,
                isDurable: IsDurable);

            DeadLetterQueueChannel = CreateChannel(sub);
        }
        
        return DeadLetterQueueChannel.Receive(ReceiveTimeout);
    }

    protected override IAmAMessageProducerSync CreateProducer(RmqPublication publication)
    {
        return CreateProducer(CreateConnection(), publication);
    }
    
    private static IAmAMessageProducerSync CreateProducer(RmqMessagingGatewayConnection connection, RmqPublication publication)
    {
        var produces = new RmqMessageProducerFactory(connection, [publication])
            .Create();

        var producer = produces.First().Value;
        
        
        return (IAmAMessageProducerSync)producer;
    }

    protected override IAmAChannelSync CreateChannel(RmqSubscription subscription)
    {
        var channel = new ChannelFactory(new RmqMessageConsumerFactory(CreateConnection()))
            .CreateSyncChannel(subscription);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            channel.Receive(TimeSpan.FromMilliseconds(100));
        }
        
        return channel;
    }

    protected override void AssertMessageAreEquals(Message expected, Message received)
    {
        Assert.Equal(expected.Header.MessageType, received.Header.MessageType);
        Assert.Equal(expected.Header.ContentType, received.Header.ContentType);
        Assert.Equal(expected.Header.CorrelationId, received.Header.CorrelationId);
        Assert.Equal(expected.Header.DataSchema, received.Header.DataSchema);
        Assert.Equal(expected.Header.PartitionKey, received.Header.PartitionKey);
        Assert.Equal(expected.Header.ReplyTo, received.Header.ReplyTo);
        Assert.Equal(expected.Header.Subject, received.Header.Subject);
        Assert.Equal(expected.Header.SpecVersion, received.Header.SpecVersion);
        Assert.Equal(expected.Header.Source, received.Header.Source);
        Assert.Equal(expected.Header.Topic, received.Header.Topic);
        Assert.Equal(expected.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"), received.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"));
        Assert.Equal(expected.Header.Type, received.Header.Type);
        Assert.Equal(expected.Body.Value, received.Body.Value);
        Assert.Equal(expected.Header.TraceParent, received.Header.TraceParent);
        Assert.Equal(expected.Header.TraceState, received.Header.TraceState);
        Assert.Equal(expected.Header.Baggage, received.Header.Baggage);
    }

    [Fact]
    public void When_a_message_consumer_throws_an_already_closed_exception_when_connecting_should_throw_channel_failure_exception()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        
        var message = CreateMessage(Publication.Topic!);
        var badReceiver = new AlreadyClosedRmqMessageConsumer(CreateConnection(), Subscription.ChannelName, message.Header.Topic, false, 1, false);

        Producer = CreateProducer(Publication);

        // Act
        Producer.Send(message);
        Thread.Sleep(DelayForReceiveMessage);
        
        // Assert
        try
        {
            badReceiver.Receive(TimeSpan.FromMilliseconds(2000));
            Assert.Fail("Expecting an ChannelFailureException");
        }
        catch (ChannelFailureException cfe)
        {
            Assert.IsType<AlreadyClosedException>(cfe.InnerException);
        }
        finally
        {
            badReceiver.Dispose();
        }
    }
    
    [Fact]
    public void When_a_message_consumer_throws_an_not_supported_exception_when_connecting_should_throw_channel_failure_exception()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        
        var message = CreateMessage(Publication.Topic!);
        var badReceiver = new NotSupportedRmqMessageConsumer(CreateConnection(), Subscription.ChannelName, message.Header.Topic, false, 1, false);
        
        Producer = CreateProducer(Publication);
        
        // Act
        Producer.Send(message);
        Thread.Sleep(DelayForReceiveMessage);
        
        try
        {
            badReceiver.Receive(TimeSpan.FromMilliseconds(2000));
            Assert.Fail("Expecting an ChannelFailureException");
        }
        catch (ChannelFailureException cfe)
        {
            Assert.IsType<NotSupportedException>(cfe.InnerException);
        }
        finally
        {
            badReceiver.Dispose();
        }
    }

    [Fact]
    public void When_a_message_consumer_throws_an_operation_interrupted_exception_when_connecting_should_throw_channel_failure_exception()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());

        var message = CreateMessage(Publication.Topic!);
        var badReceiver = new OperationInterruptedRmqMessageConsumer(CreateConnection(), Subscription.ChannelName,
            message.Header.Topic, false, 1, false);

        Producer = CreateProducer(Publication);

        // Act
        Producer.Send(message);
        Thread.Sleep(DelayForReceiveMessage);

        try
        {
            badReceiver.Receive(TimeSpan.FromMilliseconds(2000));
            Assert.Fail("Expecting an ChannelFailureException");
        }
        catch (ChannelFailureException cfe)
        {
            Assert.IsType<OperationInterruptedException>(cfe.InnerException);
        }
        finally
        {
            badReceiver.Dispose();
        }
    }

    [Fact]
    public void When_creating_quorum_consumer_with_high_availability_should_throw()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        var exception = Assert.Throws<ConfigurationException>(() =>
            new RmqMessageConsumer(rmqConnection, queueName, routingKey,
                isDurable: true,
                highAvailability: true, // This should cause the exception
                queueType: QueueType.Quorum));

        Assert.Contains("Quorum queues do not support high availability mirroring", exception.Message);
    }

    [Fact]
    public virtual void When_rejecting_a_message_due_to_queue_length_should_throw_publish_exception()
    {
        // arrange
        MaxQueueLenght = 1;
        BufferSize = 1;
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Channel = CreateChannel(Subscription);
        Producer = CreateProducer(Publication);
        
        Producer.Send(CreateMessage(Publication.Topic!));
        Producer.Send(CreateMessage(Publication.Topic!));
        
        try
        {
            Producer.Send(CreateMessage(Publication.Topic!));
            Assert.Fail("Exception an exception during publication");
        }
        catch (Exception e)
        {
            Assert.IsType<PublishException>(e);
        }
        
        // Act
        var received = Channel.Receive(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        Channel.Acknowledge(received);
        
        received = Channel.Receive(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        Channel.Acknowledge(received);
        
        received = Channel.Receive(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  received.Header.MessageType);
    }
    
    [Fact]
    public virtual void When_rejecting_to_dead_letter_queue_a_message_due_to_queue_length_should_move_to_dlq()
    {
         // arrange
        MaxQueueLenght = 1;
        BufferSize = 1;
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Channel = CreateChannel(Subscription);
        Producer = CreateProducer(Publication);
        
        Producer.Send(CreateMessage(Publication.Topic!));
        Producer.Send(CreateMessage(Publication.Topic!));
        
        try
        {
            Producer.Send(CreateMessage(Publication.Topic!));
            Assert.Fail("Exception an exception during publication");
        }
        catch (Exception e)
        {
            Assert.IsType<PublishException>(e);
        }
        
        // Act
        var received = Channel.Receive(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        Channel.Acknowledge(received);
        
        received = Channel.Receive(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        Channel.Acknowledge(received);
        
        received = Channel.Receive(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  received.Header.MessageType);

        received = GetMessageFromDeadLetterQueue(subscription: Subscription);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
    }

    [Fact]
    public void When_rejecting_a_message_to_a_dead_letter_queue_should_not_requeue()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);

        var messageOne = CreateMessage(Publication.Topic!);
        Producer.Send(messageOne);

        var received = Channel.Receive(ReceiveTimeout);
        Channel.Reject(received);
        
        received = Channel.Receive(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);
    }   
}
