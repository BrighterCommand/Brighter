using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// A factory class responsible for creating Brighter <see cref="Channel"/> instances for
/// Google Cloud Pub/Sub subscriptions. It also provides methods for deleting topics and subscriptions
/// on the Pub/Sub service.
/// </summary>
/// <param name="connection">The connection details for the Google Cloud Pub/Sub gateway.</param>
public class GcpPubSubChannelFactory(GcpMessagingGatewayConnection connection)
    : GcpPubSubMessageGateway(connection), IAmAChannelFactory
{
    private readonly GcpPubSubConsumerFactory _consumerFactory = new(connection);

    /// <summary>
    /// Creates a synchronous <see cref="IAmAChannelSync"/> for the given subscription.
    /// It ensures the subscription exists on the broker and then creates a consumer.
    /// </summary>
    /// <param name="subscription">The subscription details.</param>
    /// <returns>A synchronous Brighter channel.</returns>
    /// <exception cref="ConfigurationException">Thrown if the subscription type is not <see cref="GcpPubSubSubscription"/>.</exception>
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        if (subscription is not GcpPubSubSubscription pullSubscription)
        {
            throw new ConfigurationException("We expect a GcpSubscription as a parameter");
        }

        // Wrap the asynchronous logic in a synchronous context
        return BrighterAsyncContext.Run(async () =>
        {
            // Ensure the topic and subscription (and DLQ components, if configured) exist
            await EnsureSubscriptionExistsAsync(pullSubscription);

            // Create a synchronous channel, leveraging the consumer factory for the underlying GcpConsumer
            return new Channel(
                new ChannelName(pullSubscription.ChannelName.Value),
                pullSubscription.RoutingKey,
                _consumerFactory.Create(pullSubscription),
                pullSubscription.BufferSize);
        });
    }

    /// <summary>
    /// Creates an asynchronous <see cref="IAmAChannelAsync"/> for the given subscription.
    /// This method is an adapter that runs the preferred async creation method.
    /// </summary>
    /// <param name="subscription">The subscription details.</param>
    /// <returns>An asynchronous Brighter channel.</returns>
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        => BrighterAsyncContext.Run(async () => await CreateAsyncChannelAsync(subscription));

    /// <summary>
    /// Asynchronously creates an <see cref="IAmAChannelAsync"/> for the given subscription.
    /// It ensures the subscription exists on the broker and then creates an asynchronous consumer.
    /// </summary>
    /// <param name="subscription">The subscription details.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that returns an asynchronous Brighter channel.</returns>
    /// <exception cref="ConfigurationException">Thrown if the subscription type is not <see cref="GcpPubSubSubscription"/>.</exception>
    public async Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription,
        CancellationToken ct = default)
    {
        if (subscription is not GcpPubSubSubscription pullSubscription)
        {
            throw new ConfigurationException("We expect a GcpSubscription as a parameter");
        }

        // Ensure the topic and subscription (and DLQ components, if configured) exist
        await EnsureSubscriptionExistsAsync(pullSubscription);

        // Create an asynchronous channel, leveraging the consumer factory for the underlying GcpConsumer
        return new ChannelAsync(
            new ChannelName(pullSubscription.ChannelName.Value),
            pullSubscription.RoutingKey,
            _consumerFactory.CreateAsync(pullSubscription),
            pullSubscription.BufferSize
        );
    }

    /// <summary>
    /// Asynchronously deletes the main topic and the associated Dead Letter Topic (DLT), if configured.
    /// Not finding the topic is treated as success (idempotency).
    /// </summary>
    /// <param name="pubSubSubscription">The subscription containing the topic and DLT details.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>A task representing the asynchronous deletion operation.</returns>
    public async Task DeleteTopicAsync(GcpPubSubSubscription pubSubSubscription, CancellationToken cancellation = default)
    {
        // Get the client for topic management
        var publisherServiceApiClient = await Connection.CreatePublisherServiceApiClientAsync();

        try
        {
            // Delete the main topic
            await publisherServiceApiClient.DeleteTopicAsync(
                new DeleteTopicRequest
                {
                    TopicAsTopicName = GetTopicName(pubSubSubscription.ProjectId, pubSubSubscription.RoutingKey)
                }, CallSettings.FromCancellationToken(cancellation));

            // Delete the Dead Letter Topic (DLT) if a DeadLetterPolicy is configured
            if (pubSubSubscription.DeadLetter != null)
            {
                await publisherServiceApiClient.DeleteTopicAsync(
                    new DeleteTopicRequest
                    {
                        TopicAsTopicName = GetTopicName(pubSubSubscription.ProjectId, pubSubSubscription.DeadLetter.TopicName)
                    }, CallSettings.FromCancellationToken(cancellation));
            }
        }
        // Ignore RpcException with StatusCode.NotFound, as the topic is already gone
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
        }
    }

    /// <summary>
    /// Synchronously deletes the main topic and the associated Dead Letter Topic (DLT), if configured.
    /// Not finding the topic is treated as success (idempotency).
    /// </summary>
    /// <param name="pubSubSubscription">The subscription containing the topic and DLT details.</param>
    public void DeleteTopic(GcpPubSubSubscription pubSubSubscription)
    {
        // Get the client for topic management
        var publisherServiceApiClient = Connection.CreatePublisherServiceApiClient();
        try
        {
            // Delete the main topic
            publisherServiceApiClient.DeleteTopic(new DeleteTopicRequest
            {
                TopicAsTopicName = GetTopicName(pubSubSubscription.ProjectId, pubSubSubscription.RoutingKey.Value)
            });

            // Delete the Dead Letter Topic (DLT) if a DeadLetterPolicy is configured
            if (pubSubSubscription.DeadLetter != null)
            {
                publisherServiceApiClient.DeleteTopic(new DeleteTopicRequest
                {
                    TopicAsTopicName = GetTopicName(pubSubSubscription.ProjectId, pubSubSubscription.DeadLetter.TopicName)
                });
            }
        }
        // Ignore RpcException with StatusCode.NotFound, as the topic is already gone
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
        }
    }

    /// <summary>
    /// Asynchronously deletes the main subscription and the associated Dead Letter Subscription (DLS), if configured.
    /// Not finding the subscription is treated as success (idempotency).
    /// </summary>
    /// <param name="pubSubSubscription">The subscription details to be deleted.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>A task representing the asynchronous deletion operation.</returns>
    public async Task DeleteSubscriptionAsync(GcpPubSubSubscription pubSubSubscription, CancellationToken cancellation = default)
    {
        // Get the client for subscription management
        var publisher = await Connection.CreateSubscriberServiceApiClientAsync();
        try
        {
            // Delete the main subscription
            await publisher.DeleteSubscriptionAsync(
                new DeleteSubscriptionRequest
                {
                    SubscriptionAsSubscriptionName =
                        GetSubscriptionName(pubSubSubscription.ProjectId, pubSubSubscription.ChannelName.Value)
                }, CallSettings.FromCancellationToken(cancellation));

            // Delete the Dead Letter Subscription (DLS) if a DeadLetterPolicy is configured
            if (pubSubSubscription.DeadLetter != null)
            {
                await publisher.DeleteSubscriptionAsync(
                    new DeleteSubscriptionRequest()
                    {
                        SubscriptionAsSubscriptionName = GetSubscriptionName(pubSubSubscription.ProjectId,
                            pubSubSubscription.DeadLetter.Subscription.Value)
                    }, CallSettings.FromCancellationToken(cancellation));
            }
        }
        // Ignore RpcException with StatusCode.NotFound, as the subscription is already gone
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
        }
    }

    /// <summary>
    /// Synchronously deletes the main subscription and the associated Dead Letter Subscription (DLS), if configured.
    /// Not finding the subscription is treated as success (idempotency).
    /// </summary>
    /// <param name="pubSubSubscription">The subscription details to be deleted.</param>
    public void DeleteSubscription(GcpPubSubSubscription pubSubSubscription)
    {
        // Get the client for subscription management
        var serviceApiClient = Connection.GetOrCreateSubscriberServiceApiClient();
        try
        {
            // Delete the main subscription
            serviceApiClient.DeleteSubscription(new DeleteSubscriptionRequest
            {
                SubscriptionAsSubscriptionName =
                    GetSubscriptionName(pubSubSubscription.ProjectId, pubSubSubscription.ChannelName.Value)
            });

            // Delete the Dead Letter Subscription (DLS) if a DeadLetterPolicy is configured
            if (pubSubSubscription.DeadLetter != null)
            {
                serviceApiClient.DeleteSubscription(new DeleteSubscriptionRequest
                {
                    SubscriptionAsSubscriptionName = GetSubscriptionName(pubSubSubscription.ProjectId,
                        pubSubSubscription.DeadLetter.Subscription.Value)
                });
            }
        }
        // Ignore RpcException with StatusCode.NotFound, as the subscription is already gone
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
        }
    }
}
