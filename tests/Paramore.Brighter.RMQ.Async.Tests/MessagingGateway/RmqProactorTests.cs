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
using MyCommand = Paramore.Brighter.Base.Test.Requests.MyCommand;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

public class RmqProactorTests : MessagingGatewayProactorTests<RmqPublication, RmqSubscription>
{
    protected int? MaxQueueLenght { get; set; }
    protected TimeSpan? Ttl { get; set; }
    protected int BufferSize { get; set; } = 2;
    
    private IAmAChannelAsync? DeadLetterQueueChannel { get; set; }
    
    protected override bool HasSupportToDeadLetterQueue => true;

    protected override async Task CleanUpAsync(CancellationToken cancellationToken = default)
    {
        if (DeadLetterQueueChannel != null)
        {
            try
            {
                await DeadLetterQueueChannel.PurgeAsync(cancellationToken);
                DeadLetterQueueChannel.Dispose();
                DeadLetterQueueChannel = null;
            }
            catch 
            {
                // Ignoring any error
            }
        }
        
        await base.CleanUpAsync(cancellationToken);
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
            bufferSize: BufferSize);

        if (setupDeadLetterQueue)
        {
            GetMessageFromDeadLetterQueueAsync(subscription)
                .GetAwaiter()
                .GetResult();
        }

        return subscription;
    }

    protected override async Task<Message> GetMessageFromDeadLetterQueueAsync(RmqSubscription subscription, CancellationToken cancellationToken = default)
    {
        if (DeadLetterQueueChannel == null)
        {
            var sub = new RmqSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(Uuid.NewAsString()),
                channelName: subscription.DeadLetterChannelName,
                routingKey: subscription.DeadLetterRoutingKey,
                messagePumpType: MessagePumpType.Proactor);

            DeadLetterQueueChannel = await CreateChannelAsync(sub, cancellationToken);
        }
        
        return await DeadLetterQueueChannel.ReceiveAsync(ReceiveTimeout,cancellationToken);
    }

    protected override async Task<IAmAMessageProducerAsync> CreateProducerAsync(RmqPublication publication, CancellationToken cancellationToken = default)
    {
        return await CreateProducerAsync(Configuration.Connection, publication);
    }
    
    private static async Task<IAmAMessageProducerAsync> CreateProducerAsync(RmqMessagingGatewayConnection connection, RmqPublication publication)
    {
        var produces = await new RmqMessageProducerFactory(connection, [publication])
            .CreateAsync();

        var producer = produces.First().Value;
        
        
        return (IAmAMessageProducerAsync)producer;
    }

    protected override async Task<IAmAChannelAsync> CreateChannelAsync(RmqSubscription subscription, CancellationToken cancellationToken = default)
    {
        var channel = await new ChannelFactory(new RmqMessageConsumerFactory(Configuration.Connection))
            .CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
        
        return channel;
    }

    [Fact]
    public async Task When_a_message_consumer_throws_an_already_closed_exception_when_connecting()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        
        var message = CreateMessage(Publication.Topic!);
        var badReceiver = new AlreadyClosedRmqMessageConsumer(Configuration.Connection, Subscription.ChannelName, message.Header.Topic, false, 1, false);

        Producer = await CreateProducerAsync(Publication);

        // Act
        await Producer.SendAsync(message);
        await Task.Delay(DelayForReceiveMessage);
        
        // Assert
        try
        {
            await badReceiver.ReceiveAsync(TimeSpan.FromMilliseconds(2000));
            Assert.Fail("Expecting an ChannelFailureException");
        }
        catch (ChannelFailureException cfe)
        {
            Assert.IsType<AlreadyClosedException>(cfe.InnerException);
        }
        finally
        {
            await badReceiver.DisposeAsync();
        }
    }
    
    [Fact]
    public async Task When_a_message_consumer_throws_an_not_supported_exception_when_connecting()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        
        var message = CreateMessage(Publication.Topic!);
        var badReceiver = new NotSupportedRmqMessageConsumer(Configuration.Connection, Subscription.ChannelName, message.Header.Topic, false, 1, false);
        
        Producer = await CreateProducerAsync(Publication);
        
        // Act
        await Producer.SendAsync(message);
        await Task.Delay(DelayForReceiveMessage);
        
        try
        {
            await badReceiver.ReceiveAsync(TimeSpan.FromMilliseconds(2000));
            Assert.Fail("Expecting an ChannelFailureException");
        }
        catch (ChannelFailureException cfe)
        {
            Assert.IsType<NotSupportedException>(cfe.InnerException);
        }
        finally
        {
            await badReceiver.DisposeAsync();
        }
    }
    
    [Fact]
    public async Task When_a_message_consumer_throws_an_operation_interrupted_exception_when_connecting()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        
        var message = CreateMessage(Publication.Topic!);
        var badReceiver = new OperationInterruptedRmqMessageConsumer(Configuration.Connection, Subscription.ChannelName, message.Header.Topic, false, 1, false);
        
        Producer = await CreateProducerAsync(Publication);
        
        // Act
        await Producer.SendAsync(message);
        await Task.Delay(DelayForReceiveMessage);
        
        try
        {
            await badReceiver.ReceiveAsync(TimeSpan.FromMilliseconds(2000));
            Assert.Fail("Expecting an ChannelFailureException");
        }
        catch (ChannelFailureException cfe)
        {
            Assert.IsType<OperationInterruptedException>(cfe.InnerException);
        }
        finally
        {
            await badReceiver.DisposeAsync();
        }
    }
    
    
    [Fact]
    public void When_creating_quorum_consumer_without_durability_should_throw()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.durableexchange", durable: true)
        };

        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        var exception = Assert.Throws<ConfigurationException>(() =>
            new RmqMessageConsumer(rmqConnection, queueName, routingKey,
                isDurable: false, // This should cause the exception
                highAvailability: false,
                queueType: QueueType.Quorum));

        Assert.Contains("Quorum queues require durability to be enabled", exception.Message);
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
    public async Task When_creating_quorum_consumer_with_correct_settings_should_succeed()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        // This should not throw any exception
        await using var consumer = new RmqMessageConsumer(rmqConnection, queueName, routingKey,
            isDurable: true, // Required for quorum
            highAvailability: false, // Must be false for quorum
            queueType: QueueType.Quorum);

        await new QueueFactory(rmqConnection, queueName, new RoutingKeys(routingKey), isDurable: true, queueType: QueueType.Quorum)
            .CreateAsync();

        Assert.NotNull(consumer);
    }

    [Fact]
    public async Task When_creating_classic_consumer_with_default_settings_should_succeed()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.durableexchange", durable: true)
        };

        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        // Classic queue (default) should work with any settings
        await using var consumer = new RmqMessageConsumer(rmqConnection, queueName, routingKey,
            isDurable: false,
            highAvailability: true,
            queueType: QueueType.Classic);
        
        var message = await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(100));
        Assert.Equal(MessageType.MT_NONE, message.Single().Header.MessageType);

        Assert.NotNull(consumer);
    }
    
    [Fact]
    public async Task When_posting_a_message_to_persist_via_the_messaging_gateway()
    {
        // arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Configuration.PersistConnection, Publication);
        Channel = await CreateChannelAsync(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        
        // Act
        await Producer.SendAsync(message);
        await Task.Delay(DelayForReceiveMessage);
        var received = await Channel.ReceiveAsync(ReceiveTimeout);
        
        // Assert
        Assert.Equal(message.Header.MessageType, received.Header.MessageType);
        Assert.True(received.Persist);
        Assert.Equal(message.Header.ContentType, received.Header.ContentType);
        Assert.Equal(message.Header.CorrelationId, received.Header.CorrelationId);
        Assert.Equal(message.Header.DataSchema, received.Header.DataSchema);
        Assert.Equal(message.Header.MessageId, received.Header.MessageId);
        Assert.Equal(message.Header.PartitionKey, received.Header.PartitionKey);
        Assert.Equal(message.Header.ReplyTo, received.Header.ReplyTo);
        Assert.Equal(message.Header.Subject, received.Header.Subject);
        Assert.Equal(message.Header.SpecVersion, received.Header.SpecVersion);
        Assert.Equal(message.Header.Source, received.Header.Source);
        Assert.Equal(message.Header.Topic, received.Header.Topic);
        Assert.Equal(message.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"), received.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"));
        Assert.Equal(message.Header.Type, received.Header.Type);
        Assert.Equal(message.Body.Value, received.Body.Value);
    }
    
    [Fact]
    public async Task When_rejecting_a_message_due_to_queue_length()
    {
        // arrange
        MaxQueueLenght = 1;
        BufferSize = 1;
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Channel = await CreateChannelAsync(Subscription);
        Producer = await CreateProducerAsync(Publication);
        
        await Producer.SendAsync(CreateMessage(Publication.Topic!));
        await Producer.SendAsync(CreateMessage(Publication.Topic!));
        
        try
        {
            await Producer.SendAsync(CreateMessage(Publication.Topic!));
            Assert.Fail("Exception an exception during publication");
        }
        catch (Exception e)
        {
            Assert.IsType<PublishException>(e);
        }
        
        // Act
        var received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        await Channel.AcknowledgeAsync(received);
        
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        await Channel.AcknowledgeAsync(received);
        
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  received.Header.MessageType);
    }
    
    [Fact]
    public async Task When_rejecting_to_dead_letter_queue_a_message_due_to_queue_length()
    {
         // arrange
        MaxQueueLenght = 1;
        BufferSize = 1;
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Channel = await CreateChannelAsync(Subscription);
        Producer = await CreateProducerAsync(Publication);
        
        await Producer.SendAsync(CreateMessage(Publication.Topic!));
        await Producer.SendAsync(CreateMessage(Publication.Topic!));
        
        try
        {
            await Producer.SendAsync(CreateMessage(Publication.Topic!));
            Assert.Fail("Exception an exception during publication");
        }
        catch (Exception e)
        {
            Assert.IsType<PublishException>(e);
        }
        
        // Act
        var received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        await Channel.AcknowledgeAsync(received);
        
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        await Channel.AcknowledgeAsync(received);
        
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE,  received.Header.MessageType);

        received = await GetMessageFromDeadLetterQueueAsync(subscription: Subscription);
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
    }
    
    [Fact]
    public async Task When_resetting_a_connection_that_exists()
    {
        var connectionPool = new RmqMessageGatewayConnectionPool("MyConnectionName", 7);
        var connectionFactory = new ConnectionFactory{HostName = "localhost"};
        var originalConnection = await connectionPool.GetConnectionAsync(connectionFactory);

        await connectionPool.ResetConnectionAsync(connectionFactory);
        Assert.NotSame(originalConnection, (await connectionPool.GetConnectionAsync(connectionFactory)));
    } 
    
    [Fact]
    public async Task When_rejecting_a_message_to_a_dead_letter_queue()
    {
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);

        var messageOne = CreateMessage(Publication.Topic!);
        await Producer.SendAsync(messageOne);

        var received = await Channel.ReceiveAsync(ReceiveTimeout);
        await Channel.RejectAsync(received);
        
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);
    }
}
