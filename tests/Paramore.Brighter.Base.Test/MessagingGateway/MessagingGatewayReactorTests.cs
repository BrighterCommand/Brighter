#region License

/* The MIT License (MIT)
Copyright © 2025 Rafael Andrade

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
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Extensions;
using Xunit;
using Baggage = Paramore.Brighter.Observability.Baggage;

namespace Paramore.Brighter.Base.Test.MessagingGateway;

/// <summary>
/// Base test class for testing messaging gateway implementations with async/proactor pattern.
/// Provides infrastructure for testing message producers and channels with publication and subscription setup.
/// </summary>
/// <typeparam name="TPublication">The type of publication configuration used by the messaging gateway. Must inherit from <see cref="Publication"/>.</typeparam>
/// <typeparam name="TSubscription">The type of subscription configuration used by the messaging gateway. Must inherit from <see cref="Subscription"/>.</typeparam>
/// <remarks>
/// This base class follows the xUnit IAsyncLifetime pattern for setup and teardown.
/// Derived classes should implement the abstract methods to provide specific gateway implementations.
/// The class handles proper disposal of resources including producers, subscriptions, and channels.
/// </remarks>
// [Collection("MessagingGatewayReactor")]
public abstract class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
    where TPublication : Publication
    where TSubscription : Subscription
{
    /// <summary>
    /// Gets the publication configuration for the test.
    /// </summary>
    /// <value>The publication configuration as <typeparamref name="TPublication"/>, or null if not yet initialized.</value>
    protected TPublication? Publication { get; set; }
    
    /// <summary>
    /// Gets the subscription configuration for the test.
    /// </summary>
    /// <value>The subscription configuration as <typeparamref name="TSubscription"/>, or null if not yet initialized.</value>
    protected TSubscription? Subscription { get; set; }
    
    /// <summary>
    /// Gets the message producer for the test.
    /// </summary>
    /// <value>The message producer as <see cref="IAmAMessageProducer"/>, or null if not yet initialized.</value>
    protected IAmAMessageProducerSync? Producer { get; set; }
    
    /// <summary>
    /// Gets the async channel for receiving messages.
    /// </summary>
    /// <value>The async channel as <see cref="IAmAChannelAsync"/>, or null if not yet initialized.</value>
    protected IAmAChannelSync? Channel { get; set; }

    /// <summary>
    /// Indicates whether the messaging gateway supports delayed message delivery.
    /// </summary>
    /// <value><c>true</c> if delayed messages are supported; otherwise, <c>false</c>. Default is <c>false</c>.</value>
    /// <remarks>
    /// When <c>true</c>, tests for delayed message functionality will execute. 
    /// Override this property in derived classes to enable delayed message tests.
    /// </remarks>
    protected virtual bool HasSupportToDelayedMessages { get; } = false;
    
    /// <summary>
    /// Indicates whether the messaging gateway supports dead letter queues.
    /// </summary>
    /// <value><c>true</c> if dead letter queues are supported; otherwise, <c>false</c>. Default is <c>false</c>.</value>
    /// <remarks>
    /// When <c>true</c>, tests that verify dead letter queue functionality will execute.
    /// Override this property in derived classes to enable dead letter queue tests.
    /// </remarks>
    protected virtual bool HasSupportToDeadLetterQueue { get; } = false;
    
    /// <summary>
    /// Indicates whether the messaging gateway supports automatic movement to dead letter queue after exceeding requeue limit.
    /// </summary>
    /// <value><c>true</c> if automatic DLQ movement is supported; otherwise, <c>false</c>. Default is <c>false</c>.</value>
    /// <remarks>
    /// When <c>true</c>, tests that verify automatic dead letter queue movement after too many requeues will execute.
    /// This feature moves messages to a dead letter queue when they exceed the <see cref="Subscription.RequeueCount"/>.
    /// Override this property in derived classes to enable these tests.
    /// </remarks>
    protected virtual bool HasSupportToMoveToDeadLetterQueueAfterTooManyRetries { get; } = false;
    
    protected virtual bool HasSupportToPartitionKey { get; } = false;
    
    /// <summary>
    /// Initializes the test fixture asynchronously before each test runs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous initialization operation.</returns>
    public Task InitializeAsync()
    {
        BeforeEachTest();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Hook method called before each test runs. Override to provide custom setup logic.
    /// </summary>
    protected virtual void BeforeEachTest()
    {
    }
    
    /// <summary>
    /// Disposes the test fixture asynchronously after each test completes.
    /// </summary>
    public async Task DisposeAsync()
    {
        AfterEachTest();
        await DisposeAsync(Producer);
        await DisposeAsync(Subscription);
        
        Producer = null;
        Subscription = null;
    }

    private static async Task DisposeAsync(object? obj)
    {
        if (obj is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (obj is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Hook method called after each test completes. Override to provide custom teardown logic.
    /// </summary>
    protected virtual void AfterEachTest()
    {
        CleanUp();
    }

    /// <summary>
    /// Cleans up test resources, including purging the channel if available.
    /// </summary>
    protected virtual void CleanUp()
    {
        if (Channel != null)
        {
            try
            {
                Channel.Purge();
            }
            catch
            {
                // Ignore any error during purge
            }
        }
    }
    
    /// <summary>
    /// Creates a publication configuration for the specified routing key.
    /// </summary>
    /// <param name="routingKey">The routing key for the publication.</param>
    /// <returns>A publication configuration of type <typeparamref name="TPublication"/>.</returns>
    protected abstract TPublication CreatePublication(RoutingKey routingKey);
    
    /// <summary>
    /// Creates a subscription configuration for the specified routing key and channel name.
    /// </summary>
    /// <param name="routingKey">The routing key for the subscription.</param>
    /// <param name="channelName">The channel name for the subscription.</param>
    /// <param name="makeChannel">Strategy for handling missing channels. Default is <see cref="OnMissingChannel.Create"/>.</param>
    /// <param name="setupDeadLetterQueue">Whether to set up a dead letter queue. Default is <c>false</c>.</param>
    /// <returns>A subscription configuration of type <typeparamref name="TSubscription"/>.</returns>
    protected abstract TSubscription CreateSubscription(RoutingKey routingKey, 
        ChannelName channelName, 
        OnMissingChannel makeChannel = OnMissingChannel.Create, 
        bool setupDeadLetterQueue = false);
    
    /// <summary>
    /// Creates an async message producer for the specified publication.
    /// </summary>
    /// <param name="publication">The publication configuration.</param>
    protected abstract IAmAMessageProducerSync CreateProducer(TPublication publication);
    
    /// <summary>
    /// Creates an async channel for the specified subscription.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    protected abstract IAmAChannelSync CreateChannel(TSubscription subscription);

    /// <summary>
    /// Gets or creates a unique routing key for the test. Uses the calling test method name as the seed.
    /// </summary>
    /// <param name="testName">The name of the calling test method.</param>
    /// <returns>A <see cref="RoutingKey"/> with a unique topic name.</returns>
    protected virtual RoutingKey GetOrCreateRoutingKey([CallerMemberName] string testName = null!)
    {
        return new RoutingKey($"Topic{Uuid.New():N}");
    }

    /// <summary>
    /// Gets or creates a unique channel name for the test. Uses the calling test method name as the seed.
    /// </summary>
    /// <param name="testName">The name of the calling test method.</param>
    /// <returns>A <see cref="ChannelName"/> with a unique queue name.</returns>
    protected virtual ChannelName GetOrCreateChannelName([CallerMemberName] string testName = null!)
    {
        return new ChannelName($"Queue{Uuid.New():N}");
    }

    /// <summary>
    /// Retrieves a message from the dead letter queue for the specified subscription.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, containing the message from the dead letter queue.</returns>
    /// <exception cref="NotImplementedException">Thrown if the messaging gateway does not support dead letter queues.</exception>
    /// <remarks>
    /// Override this method in derived classes to provide implementation for retrieving messages from the dead letter queue.
    /// This method is used by tests that verify dead letter queue functionality.
    /// </remarks>
    protected virtual Message GetMessageFromDeadLetterQueue(TSubscription subscription)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a test message with CloudEvents-compliant headers.
    /// </summary>
    /// <param name="routingKey">The routing key for the message.</param>
    /// <param name="setTrace">Whether to set trace context headers (TraceParent, TraceState, Baggage). Default is <c>true</c>.</param>
    /// <returns>A <see cref="Message"/> configured with CloudEvents headers and a random body.</returns>
    /// <remarks>
    /// The created message includes CloudEvents-compliant headers such as correlation ID, data schema, reply-to, subject, source, and type.
    /// When <paramref name="setTrace"/> is <c>true</c>, the message includes OpenTelemetry trace context headers for distributed tracing.
    /// </remarks>
    protected virtual Message CreateMessage(RoutingKey routingKey, bool setTrace = true)
    {
        Baggage? baggage = null;
        TraceState? traceState = null;
        TraceParent? traceParent = null;
        if (setTrace)
        {
            baggage = new Baggage();
            baggage.LoadBaggage("userId=alice");

            traceState = new TraceState("congo=t61rcWkgMzE");
            traceParent = new TraceParent("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");
        }

        return new Message(
            new MessageHeader(
                messageId: Id.Random(),
                topic: routingKey,
                messageType: MessageType.MT_COMMAND,
                correlationId: Id.Random(),
                dataSchema: new Uri("https://example.com/storage/tenant/container", UriKind.RelativeOrAbsolute),
                replyTo: new RoutingKey($"ReplyTo{Uuid.New():N}"),
                subject: $"Subject{Uuid.New():N}",
                source: new Uri($"/component/{Uuid.New()}", UriKind.RelativeOrAbsolute),
                timeStamp: DateTimeOffset.UtcNow,
                type: new CloudEventsType($"Type{Uuid.New():N}"),
                traceState: traceState,
                traceParent: traceParent,
                baggage: baggage
            ) { DataRef = $"DataRef{Uuid.New():N}", },
            new MessageBody(Uuid.NewAsString()));
    }

    /// <summary>
    /// Gets the delay to wait after sending a message before attempting to receive it.
    /// </summary>
    /// <value>The delay as a <see cref="TimeSpan"/>. Default is 1 second.</value>
    protected virtual TimeSpan DelayForReceiveMessage => TimeSpan.FromSeconds(1);
    
    protected virtual TimeSpan DelayForRequeueMessage => TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Gets the timeout for receiving a message from the channel.
    /// </summary>
    /// <value>The timeout as a <see cref="TimeSpan"/>. Default is 1 second.</value>
    protected virtual TimeSpan ReceiveTimeout => TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Gets the delay for delayed message delivery tests.
    /// </summary>
    /// <value>The delay as a <see cref="TimeSpan"/>. Default is 5 seconds.</value>
    /// <remarks>
    /// This value is used in tests that verify delayed message functionality, determining how long a message should be delayed before delivery.
    /// </remarks>
    protected virtual TimeSpan MessageDelay => TimeSpan.FromSeconds(5);

    protected virtual void AssertMessageAreEquals(Message expected, Message received)
    {
        Assert.Equal(expected.Header.MessageType, received.Header.MessageType);
        Assert.Equal(expected.Header.ContentType, received.Header.ContentType);
        Assert.Equal(expected.Header.CorrelationId, received.Header.CorrelationId);
        Assert.Equal(expected.Header.DataSchema, received.Header.DataSchema);
        Assert.Equal(expected.Header.MessageId, received.Header.MessageId);
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
    public void When_posting_a_message_via_the_messaging_gateway_should_be_received()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        
        // Act
        Producer.Send(message);
        Thread.Sleep(DelayForReceiveMessage);
        var received = ReceiveMessage();
        
        // Assert
        AssertMessageAreEquals(message, received);
    }
    
    [Fact]
    public void When_posting_a_message_with_partition_key_via_the_messaging_gateway_should_be_received()
    {
        if (!HasSupportToPartitionKey)
        {
            return;
        }
        
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        message.Header.PartitionKey = new PartitionKey(Uuid.NewAsString());
        
        // Act
        Producer.Send(message);
        Thread.Sleep(DelayForReceiveMessage);
        var received = ReceiveMessage();
        
        // Assert
        AssertMessageAreEquals(message, received);
    }

    [Fact]
    public void When_a_message_consumer_reads_multiple_messages_should_receive_all_messages()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);

        List<Message> messages =
        [
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!)
        ];

        messages.Each(message => Producer.Send(message));
        
        Thread.Sleep(DelayForReceiveMessage);
        
        // Act
        var total = messages.Count;
        for (var i = 0; i < total; i++)
        {
            var received = ReceiveMessage();
            
            // Assert
            Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
            
            var expectedMessage = messages.FirstOrDefault(x => x.Header.MessageId == received.Header.MessageId);
            Assert.NotNull(expectedMessage);
            
            AssertMessageAreEquals(expectedMessage, received);
            Channel.Acknowledge(received);

            if ((i + 1) % Subscription.BufferSize == 0)
            {
                Thread.Sleep(DelayForReceiveMessage);
            }
        }
    }
    
    
    [Fact]
    public void When_confirming_posting_a_message_should_receive_publish_confirmation()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);

        if (Producer is not ISupportPublishConfirmation confirmation)
        {
            return;
        }

        var messageSent = false;
        confirmation.OnMessagePublished += (confirmed, _) => messageSent = confirmed; 

        var message = CreateMessage(Publication.Topic!);
        
        // Act
        Producer.Send(message);
        Thread.Sleep(DelayForReceiveMessage);
        
        // Assert
        Assert.True(messageSent);
    }
    
    [Fact]
    public void When_infrastructure_missing_and_assume_channel_should_throw_exception()
    {
        try
        {
            // Arrange
            Publication = CreatePublication(GetOrCreateRoutingKey());
            Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), OnMissingChannel.Assume);
            Producer = CreateProducer(Publication);
            Channel = CreateChannel(Subscription);
        
            var message = CreateMessage(Publication.Topic!);
        
            // Act
            Producer.Send(message);
        
            // Assert
            ReceiveMessage();
            Assert.Fail("We are expected to throw an exception");
        }
        catch
        {
            Assert.True(true);
        }
    }
    
    [Fact]
    public void When_infrastructure_missing_and_validate_channel_should_throw_exception()
    {
        try
        {
            // Arrange
            Publication = CreatePublication(GetOrCreateRoutingKey());
            Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), OnMissingChannel.Validate);
            Producer = CreateProducer(Publication);
            Channel = CreateChannel(Subscription);
        
            var message = CreateMessage(Publication.Topic!);
        
            // Act
            Producer.Send(message);
        
            // Assert
            ReceiveMessage();
            Assert.Fail("We are expected to throw an exception");
        }
        catch
        {
            Assert.True(true);
        }
    }
    
    
    [Fact]
    public void When_multiple_threads_try_to_post_a_message_at_the_same_time_should_not_throw_exception()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Producer = CreateProducer(Publication);
        
        // Act
        var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
        Parallel.ForEach(Enumerable.Range(0, 10), options, (_, _) =>
        {
            var message = CreateMessage(Publication.Topic!);
            Producer.Send(message);
        });
        
        // Assert
        Assert.True(true);
    }
    
    [Fact]
    public void When_posting_a_message_but_no_broker_created_should_throw_exception()
    {
        try
        {
            // Arrange
            Publication = CreatePublication(GetOrCreateRoutingKey());
            Publication.MakeChannels = OnMissingChannel.Validate;
            Producer = CreateProducer(Publication);
            
            // Act
            var message = CreateMessage(Publication.Topic!);
            Producer.Send(message);
            
            // Assert
            Assert.Fail("We are expected to throw an exception");
        }
        catch
        {
            Assert.True(true);
        }
    }
    
    [Fact]
    public void When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery()
    {
        if (!HasSupportToDelayedMessages)
        {
            return;
        }
        
        // arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);

        var message = CreateMessage(Publication.Topic!);
        Producer.SendWithDelay(message, MessageDelay);
        
        // Act
        var received = ReceiveMessage();
        Assert.Equal(MessageType.MT_NONE,  received.Header.MessageType);
        
        Thread.Sleep(MessageDelay);
        
        // Assert
        received = ReceiveMessage();
        AssertMessageAreEquals(message, received);
    }
    
    [Fact]
    public void When_requeing_a_failed_message_with_delay_should_receive_message_again()
    {
        if (!HasSupportToDelayedMessages)
        {
            return;
        }
        
        // arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);

        var message = CreateMessage(Publication.Topic!);
        Producer.SendWithDelay(message, MessageDelay);
        
        Thread.Sleep(MessageDelay);
        
        // Act
        var received = ReceiveMessage();
        Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
        
        Assert.True(Channel.Requeue(received));
        Thread.Sleep(DelayForRequeueMessage);
        
        // Assert
        received = ReceiveMessage();
        Channel.Acknowledge(received);
        AssertMessageAreEquals(message, received);
    }
    
    [Fact]
    public virtual void When_requeing_a_failed_message_should_receive_message_again()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        Producer.Send(message);
        
        Thread.Sleep(DelayForReceiveMessage);
        
        // Act
        var received = ReceiveMessage();
        Assert.NotEqual(MessageType.MT_QUIT,  received.Header.MessageType);

        Assert.True(Channel.Requeue(received));

        Thread.Sleep(DelayForRequeueMessage);
        received = ReceiveMessage(true);
        
        // Assert
        AssertMessageAreEquals(message, received); 
    }
    
    [Fact]
    public void When_sending_a_message_should_propagate_activity_context()
    {
        //arrange
        var builder = Sdk.CreateTracerProviderBuilder();
        var exportedActivities = new List<Activity>();

        var tracerProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("rmq-message-producer-tracer"))
            .AddInMemoryExporter(exportedActivities)
            .Build();

        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("RmqMessageProducerTests");
        parentActivity!.TraceStateString = "brighter=00f067aa0ba902b7,congo=t61rcWkgMzE";
            
        OpenTelemetry.Baggage.SetBaggage("key", "value");
        OpenTelemetry.Baggage.SetBaggage("key2", "value2");
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);
        Producer.Span = parentActivity;
            
        var message = CreateMessage(Publication.Topic!, false);
        
        //act
        Producer.Send(message);
        
        parentActivity.Stop();
        tracerProvider.ForceFlush();

        //assert
        Assert.NotNull(message.Header.TraceParent);
        Assert.Equal("brighter=00f067aa0ba902b7,congo=t61rcWkgMzE", message.Header.TraceState);
        Assert.Equal("key=value,key2=value2", message.Header.Baggage.ToString());
    }

    [Fact]
    public void When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue()
    {
        if (!HasSupportToMoveToDeadLetterQueueAfterTooManyRetries)
        {
            return;
        }
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        Producer.Send(message);

        Message? received;
        for (var i = 0; i < Subscription.RequeueCount; i++)
        {
            received = ReceiveMessage();
            Channel.Requeue(received);
        }
        
        received = ReceiveMessage();
        Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);

        received = GetMessageFromDeadLetterQueue(Subscription);
        
        // Assert
        AssertMessageAreEquals(message, received); 
    }
    
    private const int MaxRetry = 15;
    protected virtual Message ReceiveMessage(bool retryOnNoneMessage = false)
    {
        if (Channel == null)
        {
            throw new InvalidOperationException();
        }

        for (int i = 0; i < MaxRetry; i++)
        {
            try
            {
                var message = Channel.Receive(ReceiveTimeout);
                if (retryOnNoneMessage && message.Header.MessageType == MessageType.MT_NONE)
                {
                    Thread.Sleep(DelayForReceiveMessage);
                    continue;
                }

                return message;
            }
            catch (ChannelFailureException)
            {
                Thread.Sleep(DelayForReceiveMessage);
            }
        }

        return new Message();
    }
}
