using Google.Cloud.PubSub.V1;
using Google.Protobuf.Collections;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Represents Google Cloud Pub/Sub specific configuration for a message subscription (a queue).
/// This class extends the core Brighter <see cref="Subscription"/> with GCP-specific settings.
/// </summary>
public class GcpPubSubSubscription : Subscription
{
    /// <summary>
    /// Gets the Google Cloud Project ID where the subscription and its topic reside.
    /// If null, the default project ID from the connection will be used.
    /// </summary>
    public string? ProjectId { get; }

    /// <summary>
    /// Gets or sets the attributes for the associated Google Cloud Pub/Sub Topic.
    /// This is used for Topic creation and configuration during infrastructure setup.
    /// </summary>
    public TopicAttributes? TopicAttributes { get; internal set; }

    /// <summary>
    /// Gets the acknowledgment deadline in seconds for the subscription.
    /// This is the time the subscriber has to acknowledge a message before Pub/Sub redelivers it.
    /// </summary>
    public int AckDeadlineSeconds { get; }

    /// <summary>
    /// Gets a value indicating whether Pub/Sub should retain acknowledged messages.
    /// This is typically used for replay functionality.
    /// </summary>
    public bool RetainAckedMessages { get; }

    /// <summary>
    /// Gets the duration for which Pub/Sub retains messages published to the topic.
    /// This setting is applied to the subscription during creation/update.
    /// </summary>
    public TimeSpan? MessageRetentionDuration { get; }

    /// <summary>
    /// Gets a collection of key-value pairs that are attached to the subscription as labels.
    /// Labels are used for organization, billing, and resource management.
    /// </summary>
    public MapField<string, string> Labels { get; }

    /// <summary>
    /// Gets a value indicating whether messages published to the topic are delivered in the order they were published,
    /// provided they were published with an ordering key.
    /// </summary>
    public bool EnableMessageOrdering { get; }

    /// <summary>
    /// Gets a value indicating whether exactly-once delivery is enabled for the subscription.
    /// </summary>
    public bool EnableExactlyOnceDelivery { get; }

    /// <summary>
    /// Gets the configuration for forwarding message snapshots to a Google Cloud Storage bucket.
    /// This is used for data export/backup functionality.
    /// </summary>
    public CloudStorageConfig? Storage { get; }

    /// <summary>
    /// Gets the subscription's expiration policy.
    /// If set, the subscription will automatically be deleted after a period of inactivity.
    /// </summary>
    public ExpirationPolicy? ExpirationPolicy { get; }

    /// <summary>
    /// Gets the configuration for the Dead Letter Policy (DLQ).
    /// If set, messages that fail processing will be forwarded to a specified dead letter topic.
    /// </summary>
    public DeadLetterPolicy? DeadLetter { get; }

    /// <summary>
    /// Gets the maximum delay time for exponential backoff retry policy when a message is requeued.
    /// This works in conjunction with <see cref="Subscription.RequeueDelay"/> which is the minimum backoff.
    /// </summary>
    public TimeSpan MaxRequeueDelay { get; }

    /// <summary>
    /// Gets the time provider used for time-related operations (e.g., purging).
    /// </summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the message consumption mode: <see cref="SubscriptionMode.Stream"/> (default) or <see cref="SubscriptionMode.Pull"/>.
    /// </summary>
    public SubscriptionMode SubscriptionMode { get; }

    /// <summary>
    /// Gets an action to configure the <see cref="SubscriberClientBuilder"/> used for the streaming consumer.
    /// This allows for advanced customization of the underlying streaming client configuration.
    /// </summary>
    public Action<SubscriberClientBuilder>? StreamingConfiguration { get; }

    /// <summary>
    /// 
    /// </summary>
    public string? SubscriberMember { get; }

    /// <summary>
    /// Gets the type of the channel factory used to create this channel.
    /// For GCP, this is always <see cref="GcpPubSubConsumerFactory"/>.
    /// </summary>
    public override Type ChannelFactoryType => typeof(GcpPubSubConsumerFactory);

    /// <summary>
    /// Initializes a new instance of the <see cref="GcpPubSubSubscription"/> class with all Pub/Sub specific parameters.
    /// </summary>
    public GcpPubSubSubscription(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey,
        Type? requestType = null, Func<Message, Type>? getRequestType = null, int bufferSize = 1,
        int noOfPerformers = 1, TimeSpan? timeOut = null,
        int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0,
        TimeSpan? unacceptableMessageLimitWindow = null,
        MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null,
        string? projectId = null, TopicAttributes? topicAttributes = null, int ackDeadlineSeconds = 30,
        bool retainAckedMessages = false, TimeSpan? messageRetentionDuration = null,
        MapField<string, string>? labels = null,
        bool enableMessageOrdering = false, bool enableExactlyOnceDelivery = false, CloudStorageConfig? storage = null,
        ExpirationPolicy? expirationPolicy = null, DeadLetterPolicy? deadLetter = null,
        TimeSpan? maxRequeueDelay = null,
        TimeProvider? timeProvider = null, SubscriptionMode subscriptionMode = SubscriptionMode.Stream,
        Action<SubscriberClientBuilder>? streamingConfiguration = null,
        string? subscriberMember = null)
        : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize,
            noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType,
            channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay, unacceptableMessageLimitWindow)
    {
        Labels = labels ?? new MapField<string, string>();
        MaxRequeueDelay = maxRequeueDelay ?? TimeSpan.FromSeconds(600); // Default to 10 minutes
        TimeProvider = timeProvider ?? TimeProvider.System;
        DeadLetter = deadLetter;
        ExpirationPolicy = expirationPolicy;
        Storage = storage;
        EnableMessageOrdering = enableMessageOrdering;
        EnableExactlyOnceDelivery = enableExactlyOnceDelivery;
        MessageRetentionDuration = messageRetentionDuration;
        RetainAckedMessages = retainAckedMessages;
        ProjectId = projectId;
        TopicAttributes = topicAttributes;
        AckDeadlineSeconds = ackDeadlineSeconds;
        SubscriptionMode = subscriptionMode;
        StreamingConfiguration = streamingConfiguration;
        SubscriberMember = subscriberMember;
    }
}

/// <summary>
/// Represents Google Cloud Pub/Sub specific configuration for a message subscription, strongly typed to a request type.
/// This simplifies setup by automatically associating the request type with the subscription.
/// </summary>
/// <typeparam name="T">The type of <see cref="IRequest"/> associated with this subscription.</typeparam>
public class GcpPubSubSubscription<T> : GcpPubSubSubscription
    where T : class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GcpPubSubSubscription{T}"/> class, automatically setting the request type.
    /// </summary>
    public GcpPubSubSubscription(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey,
        Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1,
        TimeSpan? timeOut = null,
        int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0,
        TimeSpan? unacceptableMessageLimitWindow = null,
        MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null, string? projectId = null, TopicAttributes? topicAttributes = null,
        int ackDeadlineSeconds = 30,
        bool retainAckedMessages = false, TimeSpan? messageRetentionDuration = null,
        MapField<string, string>? labels = null,
        bool enableMessageOrdering = false, bool enableExactlyOnceDelivery = false, CloudStorageConfig? storage = null,
        ExpirationPolicy? expirationPolicy = null, DeadLetterPolicy? deadLetter = null,
        TimeSpan? maxRequeueDelay = null,
        TimeProvider? timeProvider = null, SubscriptionMode subscriptionMode = SubscriptionMode.Stream,
        string? subscriberMember = null)
        : base(subscriptionName, channelName, routingKey, typeof(T), getRequestType, bufferSize,
            noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit,
            unacceptableMessageLimitWindow, messagePumpType,
            channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay, projectId, topicAttributes,
            ackDeadlineSeconds, retainAckedMessages, messageRetentionDuration, labels, enableMessageOrdering,
            enableExactlyOnceDelivery, storage, expirationPolicy, deadLetter, maxRequeueDelay, timeProvider,
            subscriptionMode: subscriptionMode, subscriberMember: subscriberMember)
    {
    }
}
