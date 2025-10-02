using System.Collections.Concurrent;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// A factory class responsible for creating concrete implementations of <see cref="IAmAMessageConsumerSync"/>
/// or <see cref="IAmAMessageConsumerAsync"/> for Google Cloud Pub/Sub.
/// It handles the logic for creating or retrieving shared streaming consumers and creating
/// dedicated pull consumers, as well as ensuring the underlying subscription exists.
/// </summary>
/// <param name="connection">The connection details for the Google Cloud Pub/Sub gateway.</param>
public class GcpPubSubConsumerFactory(GcpMessagingGatewayConnection connection)
    : GcpPubSubMessageGateway(connection), IAmAMessageConsumerFactory
{
    private readonly GcpMessagingGatewayConnection _connection = connection;

    /// <summary>
    /// Creates a synchronous message consumer for the given subscription.
    /// This method is primarily an adapter that wraps the async creation.
    /// </summary>
    /// <param name="subscription">The subscription details.</param>
    /// <returns>An instance of <see cref="IAmAMessageConsumerSync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown if the subscription type is not <see cref="GcpPubSubSubscription"/>.</exception>
    public IAmAMessageConsumerSync Create(Subscription subscription)
    {
        if (subscription is not GcpPubSubSubscription pubSubSubscription)
        {
            throw new ConfigurationException(
                "We are expecting a PubSubSubscription or PubSubSubscription<T> as parameter");
        }

        // Wrap the asynchronous creation method to allow synchronous consumption
        return BrighterAsyncContext.Run(async () => (IAmAMessageConsumerSync)await CreateAsync(pubSubSubscription));
    }

    /// <summary>
    /// Creates an asynchronous message consumer for the given subscription.
    /// This method is primarily an adapter that wraps the underlying async creation.
    /// </summary>
    /// <param name="subscription">The subscription details.</param>
    /// <returns>An instance of <see cref="IAmAMessageConsumerAsync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown if the subscription type is not <see cref="GcpPubSubSubscription"/>.</exception>
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
    {
        if (subscription is not GcpPubSubSubscription pubSubSubscription)
        {
            throw new ConfigurationException(
                "We are expecting a PubSubSubscription or PubSubSubscription<T> as parameter");
        }

        // Wrap the asynchronous creation method to allow consumption within Brighter's async context
        return BrighterAsyncContext.Run(async () => (IAmAMessageConsumerAsync)await CreateAsync(pubSubSubscription));
    }

    /// <summary>
    /// A thread-safe dictionary to cache and share <see cref="GcpStreamConsumer"/> instances
    /// for subscriptions using <see cref="SubscriptionMode.Stream"/>.
    /// </summary>
    private static readonly ConcurrentDictionary<GcpPubSubSubscription, GcpStreamConsumer> s_consumers = new();

    /// <summary>
    /// Internal asynchronous method to create the concrete consumer implementation based on subscription mode.
    /// </summary>
    /// <param name="pubSubSubscription">The Google Cloud Pub/Sub specific subscription details.</param>
    /// <returns>A task that returns an <see cref="IAmAMessageConsumerSync"/> or <see cref="IAmAMessageConsumerAsync"/> compatible object.</returns>
    private async Task<object> CreateAsync(GcpPubSubSubscription pubSubSubscription)
    {
        // Ensure the Topic and Subscription exist on Google Cloud Pub/Sub before attempting to consume
        await EnsureSubscriptionExistsAsync(pubSubSubscription);

        // Construct the canonical Google Pub/Sub SubscriptionName
        var subscriptionName =
            Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription(
                pubSubSubscription.ProjectId ?? _connection.ProjectId, pubSubSubscription.ChannelName.Value);

        // Check if the consumer should use dedicated Pull mode
        if (pubSubSubscription.SubscriptionMode == SubscriptionMode.Pull)
        {
            // Create a new, non-shared consumer that uses the Pull API for each request
            return new GcpPullMessageConsumer(_connection, subscriptionName,
                pubSubSubscription.BufferSize, pubSubSubscription.TimeProvider);
        }

        // If not Pull, use Stream mode. Stream mode consumers are shared per subscription to manage
        // a single long-lived gRPC streaming connection.
        var consumer = s_consumers.GetOrAdd(pubSubSubscription, sub => new GcpStreamConsumer(CreateSubscriberClient(
            subscriptionName,
            sub.BufferSize * sub.NoOfPerformers,
            sub.StreamingConfiguration ?? _connection.StreamConfiguration)));

        // Start the shared stream consumer to begin receiving messages from Google Cloud Pub/Sub
        consumer.Start();

        // Return a wrapper consumer that delegates to the shared stream consumer.
        // Each Brighter 'performer' will get its own wrapper consumer.
        return new GcpPubSubStreamMessageConsumer(
            _connection,
            consumer,
            subscriptionName,
            pubSubSubscription.TimeProvider);
    }

    private Google.Cloud.PubSub.V1.SubscriberClient CreateSubscriberClient(Google.Cloud.PubSub.V1.SubscriptionName subscriptionName,
        long maxInFlightMessages,
        Action<SubscriberClientBuilder>? configure)
    {
        var builder = new SubscriberClientBuilder
        {
            SubscriptionName = subscriptionName,
            Credential = _connection.Credential
        };

        configure?.Invoke(builder);

        builder.Settings ??= new SubscriberClient.Settings();
        builder.Settings.FlowControlSettings = new FlowControlSettings(
            maxOutstandingElementCount: maxInFlightMessages,
            maxOutstandingByteCount: null);

        return builder.Build();
    }
}
