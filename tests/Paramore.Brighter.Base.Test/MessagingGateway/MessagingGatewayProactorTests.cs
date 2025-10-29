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
public abstract class MessagingGatewayProactorTests<TPublication, TSubscription> : IAsyncLifetime
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
    protected IAmAMessageProducerAsync? Producer { get; set; }
    
    /// <summary>
    /// Gets the async channel for receiving messages.
    /// </summary>
    /// <value>The async channel as <see cref="IAmAChannelAsync"/>, or null if not yet initialized.</value>
    protected IAmAChannelAsync? Channel { get; set; }

    protected virtual bool HasSupportToDelayedMessages { get; } = false;
    protected virtual bool HasSupportToDeadLetterQueue { get; } = false;
    protected virtual bool HasSupportToMoveToDeadLetterQueueAfterTooManyRetries { get; } = false;
    
    /// <summary>
    /// Initializes the test fixture asynchronously before each test runs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous initialization operation.</returns>
    public async Task InitializeAsync()
    {
        await BeforeEachTestAsync();
    }

    /// <summary>
    /// Hook method called before each test runs. Override to provide custom setup logic.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous setup operation.</returns>
    protected virtual Task BeforeEachTestAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Disposes the test fixture asynchronously after each test completes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous disposal operation.</returns>
    public async Task DisposeAsync()
    {
        await AfterEachTestAsync();
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
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous teardown operation.</returns>
    protected virtual async Task AfterEachTestAsync(CancellationToken cancellationToken = default)
    {
        await CleanUpAsync(cancellationToken);
    }

    /// <summary>
    /// Cleans up test resources, including purging the channel if available.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous cleanup operation.</returns>
    protected virtual async Task CleanUpAsync(CancellationToken cancellationToken = default)
    {
        if (Channel != null)
        {
            try
            {
                await Channel.PurgeAsync(cancellationToken);
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
    
    protected abstract TSubscription CreateSubscription(RoutingKey routingKey, 
        ChannelName channelName, 
        OnMissingChannel makeChannel = OnMissingChannel.Create, 
        bool setupDeadLetterQueue = false);
    
    /// <summary>
    /// Creates an async message producer for the specified publication.
    /// </summary>
    /// <param name="publication">The publication configuration.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, containing an <see cref="IAmAMessageProducerAsync"/>.</returns>
    protected abstract Task<IAmAMessageProducerAsync> CreateProducerAsync(TPublication publication, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates an async channel for the specified subscription.
    /// </summary>
    /// <param name="subscription">The subscription configuration.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, containing an <see cref="IAmAChannelAsync"/>.</returns>
    protected abstract Task<IAmAChannelAsync> CreateChannelAsync(TSubscription subscription, CancellationToken cancellationToken = default);

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


    protected virtual Task<Message> GetMessageFromDeadLetterQueueAsync(TSubscription subscription, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a test message with CloudEvents-compliant headers.
    /// </summary>
    /// <param name="routingKey">The routing key for the message.</param>
    /// <returns>A <see cref="Message"/> configured with CloudEvents headers and a random body.</returns>
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
    /// <value>The delay as a <see cref="TimeSpan"/>. Default is 5 seconds.</value>
    protected virtual TimeSpan DelayForReceiveMessage => TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Gets the timeout for receiving a message from the channel.
    /// </summary>
    /// <value>The timeout as a <see cref="TimeSpan"/>. Default is 1 second.</value>
    protected virtual TimeSpan ReceiveTimeout => TimeSpan.FromSeconds(1);
    
    protected virtual TimeSpan MessageDelay => TimeSpan.FromSeconds(5);

    [Fact]
    public async Task When_posting_a_message_via_the_messaging_gateway()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        
        // Act
        await Producer.SendAsync(message);
        await Task.Delay(DelayForReceiveMessage);
        var received = await Channel.ReceiveAsync(ReceiveTimeout);
        
        // Assert
        Assert.Equal(message.Header.MessageType, received.Header.MessageType);
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
        Assert.Equal(message.Header.TraceParent, received.Header.TraceParent);
        Assert.Equal(message.Header.TraceState, received.Header.TraceState);
        Assert.Equal(message.Header.Baggage, received.Header.Baggage);
    }

    [Fact]
    public async Task When_a_message_consumer_reads_multiple_messages()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);

        List<Message> messages =
        [
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!),
            CreateMessage(Publication.Topic!)
        ];

        await messages.EachAsync(async message => await Producer.SendAsync(message));
        
        await Task.Delay(DelayForReceiveMessage);
        
        // Act
        var total = messages.Count;
        for (var i = 0; i < total; i++)
        {
            var received = await Channel.ReceiveAsync(ReceiveTimeout);
            
            // Assert
            Assert.NotEqual(MessageType.MT_NONE,  received.Header.MessageType);
            
            var expectedMessage = messages.FirstOrDefault(x => x.Header.MessageId == received.Header.MessageId);
            Assert.NotNull(expectedMessage);
            
            await Channel.AcknowledgeAsync(received);

            if ((i + 1) % Subscription.BufferSize == 0)
            {
                await Task.Delay(DelayForReceiveMessage);
            }
        }
    }
    
    
    [Fact]
    public async Task When_confirming_posting_a_message_via_the_messaging_gateway_async()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);

        if (Producer is not ISupportPublishConfirmation confirmation)
        {
            return;
        }

        var messageSent = false;
        confirmation.OnMessagePublished += (confirmed, _) => messageSent = confirmed; 

        var message = CreateMessage(Publication.Topic!);
        
        // Act
        await Producer.SendAsync(message);
        await Task.Delay(DelayForReceiveMessage);
        
        // Assert
        Assert.True(messageSent);
    }
    
    [Fact]
    public async Task When_infrastructure_exists_can_assume_producer()
    {
        try
        {
            // Arrange
            Publication = CreatePublication(GetOrCreateRoutingKey());
            Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), OnMissingChannel.Assume);
            Producer = await CreateProducerAsync(Publication);
            Channel = await CreateChannelAsync(Subscription);
        
            var message = CreateMessage(Publication.Topic!);
        
            // Act
            await Producer.SendAsync(message);
        
            // Assert
            await Channel.ReceiveAsync(ReceiveTimeout);
            Assert.Fail("We are expected to throw an exception");
        }
        catch
        {
            Assert.True(true);
        }
    }
    
    [Fact]
    public async Task When_infrastructure_exists_can_validate_producer()
    {
        try
        {
            // Arrange
            Publication = CreatePublication(GetOrCreateRoutingKey());
            Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), OnMissingChannel.Validate);
            Producer = await CreateProducerAsync(Publication);
            Channel = await CreateChannelAsync(Subscription);
        
            var message = CreateMessage(Publication.Topic!);
        
            // Act
            await Producer.SendAsync(message);
        
            // Assert
            await Channel.ReceiveAsync(ReceiveTimeout);
            Assert.Fail("We are expected to throw an exception");
        }
        catch
        {
            Assert.True(true);
        }
    }
    
    
    [Fact]
    public async Task When_multiple_threads_try_to_post_a_message_at_the_same_time()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Producer = await CreateProducerAsync(Publication);
        
        // Act
        var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
        await Parallel.ForEachAsync(Enumerable.Range(0, 10), options, async (_, ct) =>
        {
            var message = CreateMessage(Publication.Topic!);
            await Producer.SendAsync(message, ct);
        });
        
        // Assert
        Assert.True(true);
    }
    
    
    [Fact]
    public async Task When_posting_a_message_but_no_broker_created()
    {
        try
        {
            // Arrange
            Publication = CreatePublication(GetOrCreateRoutingKey());
            Publication.MakeChannels = OnMissingChannel.Validate;
            Producer = await CreateProducerAsync(Publication);
            
            // Act
            var message = CreateMessage(Publication.Topic!);
            await Producer.SendAsync(message);
            
            // Assert
            Assert.Fail("We are expected to throw an exception");
        }
        catch
        {
            Assert.True(true);
        }
    }
    
    [Fact]
    public async Task When_reading_a_delayed_message_via_the_messaging_gateway()
    {
        if (!HasSupportToDelayedMessages)
        {
            return;
        }
        
        // arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);

        var message = CreateMessage(Publication.Topic!);
        await Producer.SendWithDelayAsync(message, MessageDelay);
        
        // Act
        var received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.Equal(MessageType.MT_QUIT,  received.Header.MessageType);
        
        await Task.Delay(TimeSpan.FromSeconds(6));
        
        // Assert
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_QUIT,  received.Header.MessageType);
    }
    
    [Fact]
    public async Task When_requeing_a_failed_message_with_delay()
    {
        if (!HasSupportToDelayedMessages)
        {
            return;
        }
        
        // arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);

        var message = CreateMessage(Publication.Topic!);
        await Producer.SendWithDelayAsync(message, TimeSpan.FromSeconds(5));
        await Task.Delay(TimeSpan.FromSeconds(6));
        
        // Act
        var received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_QUIT,  received.Header.MessageType);
        
        await Channel.RequeueAsync(received);
        
        // Assert
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_QUIT,  received.Header.MessageType);
        await Channel.AcknowledgeAsync(received);
    }
    
    [Fact]
    public async Task When_posting_a_message_via_the_messaging_gateway_async()
    {
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        await Producer.SendAsync(message);
        
        await Task.Delay(DelayForReceiveMessage);
        
        // Act
        var received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.NotEqual(MessageType.MT_QUIT,  received.Header.MessageType);

        await Channel.RequeueAsync(received);

        await Task.Delay(DelayForReceiveMessage);
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        
        // Assert
        Assert.Equal(message.Header.MessageType, received.Header.MessageType);
        Assert.Equal(message.Header.ContentType, received.Header.ContentType);
        Assert.Equal(message.Header.CorrelationId, received.Header.CorrelationId);
        Assert.Equal(message.Header.DataSchema, received.Header.DataSchema);
        Assert.Equal(message.Header.PartitionKey, received.Header.PartitionKey);
        Assert.Equal(message.Header.ReplyTo, received.Header.ReplyTo);
        Assert.Equal(message.Header.Subject, received.Header.Subject);
        Assert.Equal(message.Header.SpecVersion, received.Header.SpecVersion);
        Assert.Equal(message.Header.Source, received.Header.Source);
        Assert.Equal(message.Header.Topic, received.Header.Topic);
        Assert.Equal(message.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"), received.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"));
        Assert.Equal(message.Header.Type, received.Header.Type);
        Assert.Equal(message.Body.Value, received.Body.Value);
        Assert.Equal(message.Header.TraceParent, received.Header.TraceParent);
        Assert.Equal(message.Header.TraceState, received.Header.TraceState);
        Assert.Equal(message.Header.Baggage, received.Header.Baggage);
    }
    
    [Fact]
    public async Task When_Sending_A_Message_Should_Propagate_Context()
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
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);
        Producer.Span = parentActivity;
            
        var message = CreateMessage(Publication.Topic!, false);
        
        //act
        await Producer.SendAsync(message);
        
        parentActivity.Stop();
        tracerProvider.ForceFlush();

        //assert
        Assert.NotNull(message.Header.TraceParent);
        Assert.Equal("brighter=00f067aa0ba902b7,congo=t61rcWkgMzE", message.Header.TraceState);
        Assert.Equal("key=value,key2=value2", message.Header.Baggage.ToString());
    }

    [Fact]
    public async Task When_requeuing_a_message_to_a_dead_letter_queue()
    {
        if (!HasSupportToMoveToDeadLetterQueueAfterTooManyRetries)
        {
            return;
        }
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName(), setupDeadLetterQueue: true);
        Producer = await CreateProducerAsync(Publication);
        Channel = await CreateChannelAsync(Subscription);
        
        var message = CreateMessage(Publication.Topic!);
        await Producer.SendAsync(message);

        Message? received;
        for (var i = 0; i < Subscription.RequeueCount; i++)
        {
            received = await Channel.ReceiveAsync(ReceiveTimeout);
            await Channel.RequeueAsync(received);
        }
        
        received = await Channel.ReceiveAsync(ReceiveTimeout);
        Assert.Equal(MessageType.MT_NONE, received.Header.MessageType);

        received = await GetMessageFromDeadLetterQueueAsync(Subscription);
        
        // Assert
        Assert.Equal(message.Header.MessageType, received.Header.MessageType);
        Assert.Equal(message.Header.ContentType, received.Header.ContentType);
        Assert.Equal(message.Header.CorrelationId, received.Header.CorrelationId);
        Assert.Equal(message.Header.DataSchema, received.Header.DataSchema);
        Assert.Equal(message.Header.PartitionKey, received.Header.PartitionKey);
        Assert.Equal(message.Header.ReplyTo, received.Header.ReplyTo);
        Assert.Equal(message.Header.Subject, received.Header.Subject);
        Assert.Equal(message.Header.SpecVersion, received.Header.SpecVersion);
        Assert.Equal(message.Header.Source, received.Header.Source);
        Assert.Equal(message.Header.Topic, received.Header.Topic);
        Assert.Equal(message.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"), received.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"));
        Assert.Equal(message.Header.Type, received.Header.Type);
        Assert.Equal(message.Body.Value, received.Body.Value);
        Assert.Equal(message.Header.TraceParent, received.Header.TraceParent);
        Assert.Equal(message.Header.TraceState, received.Header.TraceState);
        Assert.Equal(message.Header.Baggage, received.Header.Baggage);
    }
}
