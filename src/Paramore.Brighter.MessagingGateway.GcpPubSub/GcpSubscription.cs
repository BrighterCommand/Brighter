using Google.Cloud.PubSub.V1;
using Google.Protobuf.Collections;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Represents the configuration settings for a Google Cloud Pub/Sub subscription, 
/// extending the core Brighter <see cref="Subscription"/> with GCP-specific properties.
/// </summary>
public class GcpSubscription : Subscription
{
    /// <summary>
    /// Gets or sets the Google Cloud Project ID where the subscription resides or will be created.
    /// </summary>
    /// <value>The GCP Project ID.</value>
    public string? ProjectId { get; }

    /// <summary>
    /// Gets or sets the attributes to be applied to the associated Pub/Sub Topic.
    /// </summary>
    /// <value>A <see cref="TopicAttributes"/> object containing topic configuration, typically used if Brighter needs to create the topic.</value>
    /// <remarks>This might contain settings like schema or KMS key name for the Topic itself.</remarks>
    public TopicAttributes? TopicAttributes { get; internal set; }

    /// <summary>
    /// Gets or sets the acknowledgment deadline for messages pulled from this subscription, in seconds.
    /// </summary>
    /// <value>The maximum time after receiving a message that the subscriber must acknowledge it before Pub/Sub attempts redelivery. Must be between 10 and 600 seconds.</value>
    /// <remarks>
    /// If not set, the Pub/Sub default (usually 30 seconds) applies when the subscription is created.
    /// </remarks>
    public int AckDeadlineSeconds { get; } 

    /// <summary>
    /// Gets or sets a value indicating whether to retain acknowledged messages.
    /// </summary>
    /// <value><see langword="true"/> to retain acknowledged messages for the duration specified by <see cref="MessageRetentionDuration"/>; otherwise, <see langword="false"/>.</value>
    /// <remarks>Useful for features like 'seek to a time'. Defaults to <see langword="false"/>.</remarks>
    public bool RetainAckedMessages { get; }

    /// <summary>
    /// Gets or sets the duration for which Pub/Sub retains unacknowledged messages.
    /// </summary>
    /// <value>A <see cref="TimeSpan"/> representing the retention duration. Can range from 10 minutes to 7 days.</value>
    /// <remarks>
    /// If <see cref="RetainAckedMessages"/> is true, acknowledged messages are also retained for this duration.
    /// If not set, the Pub/Sub default (usually 7 days) applies when the subscription is created.
    /// </remarks>
    public TimeSpan? MessageRetentionDuration { get; }

    /// <summary>
    /// Gets or sets the collection of labels to apply to the Pub/Sub subscription resource.
    /// </summary>
    /// <value>A <see cref="MapField{TKey, TValue}"/> containing key-value string pairs.</value>
    /// <remarks>Labels can be used for organizing and filtering GCP resources.</remarks>
    public MapField<string, string> Labels { get; } 

    /// <summary>
    /// Gets or sets a value indicating whether to enable message ordering.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if Pub/Sub should deliver messages with the same ordering key in the order they were published;
    /// otherwise, <see langword="false"/>.
    /// </value>
    /// <remarks>Requires the publisher to set ordering keys on messages. Defaults to <see langword="false"/>.</remarks>
    public bool EnableMessageOrdering { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable exactly-once delivery semantics for this subscription.
    /// </summary>
    /// <value><see langword="true"/> to enable exactly-once delivery; otherwise, <see langword="false"/>.</value>
    /// <remarks>Provides stronger guarantees but may impact performance. Defaults to <see langword="false"/>.</remarks>
    public bool EnableExactlyOnceDelivery { get; }

    /// <summary>
    /// Gets or sets the configuration for exporting acknowledged messages to Google Cloud Storage.
    /// </summary>
    /// <value>A <see cref="CloudStorageConfig"/> object or <see langword="null"/> if not configured.</value>
    /// <remarks>This feature is typically used for loading data into BigQuery via subscriptions.</remarks>
    public CloudStorageConfig? Storage { get; }

    /// <summary>
    /// Gets or sets the policy governing the automatic deletion of the subscription based on inactivity.
    /// </summary>
    /// <value>
    /// An <see cref="ExpirationPolicy"/> object specifying the conditions under which the subscription
    /// expires (e.g., TTL after no usage), or <see langword="null"/> for no expiration.
    /// </value>
    public ExpirationPolicy? ExpirationPolicy { get; }

    /// <summary>
    /// Gets or sets the full name of the Pub/Sub topic to be used as the dead-letter topic.
    /// </summary>
    /// <value>
    /// The full topic name (e.g., "projects/my-project/topics/my-dead-letter-topic") or <see langword="null"/>
    /// if no dead-lettering is configured.
    /// </value>
    /// <remarks>
    /// Messages that fail delivery a specified number of times (configured separately on the subscription) will
    /// be sent here. Requires appropriate permissions.
    /// </remarks>
    public DeadLetterPolicy? DeadLetter { get; }
    
    /// <summary>
    /// Gets or sets the maximum delay between message redelivery attempts when using Pub/Sub's default retry policy (exponential backoff).
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> representing the maximum backoff delay. Defaults to 600 seconds (10 minutes).
    /// </value>
    /// <remarks>
    /// This corresponds to the `maximum_backoff` setting in the Pub/Sub
    /// <see href="https://cloud.google.com/pubsub/docs/reference/rest/v1/projects.subscriptions#retrypolicy">RetryPolicy</see>.
    /// </remarks>
    public TimeSpan MaxRequeueDelay { get; }

    /// <summary>
    /// Gets or sets the <see cref="TimeProvider"/> used for time-related operations (e.g., calculating expiration).
    /// </summary>
    /// <value>Defaults to <see cref="TimeProvider.System"/>.</value>
    public TimeProvider TimeProvider { get; } 
    
    /// <summary>
    /// Gets or sets the message delivery mechanism to use for this subscription.
    /// </summary>
    /// <value>A <see cref="SubscriptionMode"/> value, typically <c>Stream</c> for high-performance consumption.</value>
    public SubscriptionMode SubscriptionMode { get; }

    /// <inheritdoc />
    public override Type ChannelFactoryType => typeof(GcpConsumerFactory);

    /// <summary>
    /// Initializes a new instance of the <see cref="GcpSubscription"/> class with specific Pub/Sub and Brighter settings.
    /// </summary>
    /// <param name="subscriptionName">The name of the Pub/Sub subscription.</param>
    /// <param name="channelName">The name of the Brighter channel.</param>
    /// <param name="routingKey">The topic name/routing key.</param>
    /// <param name="requestType">The type of the request expected on this subscription (optional).</param>
    /// <param name="getRequestType">A function to dynamically determine the request type from the message (optional).</param>
    /// <param name="bufferSize">The number of messages to buffer per performer (for internal channel control).</param>
    /// <param name="noOfPerformers">The number of concurrent performers (threads/tasks) processing messages.</param>
    /// <param name="timeOut">The timeout for a single receive operation.</param>
    /// <param name="requeueCount">The number of times a message should be redelivered before being considered unacceptable.</param>
    /// <param name="requeueDelay">The initial delay for requeueing a message after rejection.</param>
    /// <param name="unacceptableMessageLimit">The limit for unacceptable messages before the pump shuts down.</param>
    /// <param name="messagePumpType">The type of message pump to use.</param>
    /// <param name="channelFactory">A custom channel factory (optional).</param>
    /// <param name="makeChannels">Defines whether to create the channel if missing.</param>
    /// <param name="emptyChannelDelay">The delay when the channel is empty.</param>
    /// <param name="channelFailureDelay">The delay when the channel fails.</param>
    /// <param name="projectId">The Google Cloud Project ID where the subscription resides.</param>
    /// <param name="topicAttributes">Attributes to be applied to the associated Pub/Sub Topic upon creation.</param>
    /// <param name="ackDeadlineSeconds">The acknowledgment deadline in seconds (10 to 600).</param>
    /// <param name="retainAckedMessages">Whether to retain acknowledged messages.</param>
    /// <param name="messageRetentionDuration">The duration for message retention.</param>
    /// <param name="labels">Key-value labels to apply to the subscription resource.</param>
    /// <param name="enableMessageOrdering">Whether to enable message ordering.</param>
    /// <param name="enableExactlyOnceDelivery">Whether to enable exactly-once delivery.</param>
    /// <param name="storage">Configuration for exporting acknowledged messages to Google Cloud Storage.</param>
    /// <param name="expirationPolicy">Policy governing automatic subscription deletion based on inactivity.</param>
    /// <param name="deadLetter">Policy for dead-lettering messages that fail delivery too many times.</param>
    /// <param name="maxRequeueDelay">The maximum delay for exponential backoff retries.</param>
    /// <param name="timeProvider">The time provider used for time-related calculations.</param>
    /// <param name="subscriptionMode">The delivery mechanism to use (Stream or Pull).</param>
    public GcpSubscription(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey, 
        Type? requestType = null, Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null,
        int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null,
        string? projectId = null, TopicAttributes? topicAttributes = null, int ackDeadlineSeconds = 30,
        bool retainAckedMessages = false, TimeSpan? messageRetentionDuration = null, MapField<string, string>? labels = null, 
        bool enableMessageOrdering = false, bool enableExactlyOnceDelivery = false, CloudStorageConfig? storage = null,
        ExpirationPolicy? expirationPolicy = null, DeadLetterPolicy? deadLetter = null, TimeSpan? maxRequeueDelay = null,
        TimeProvider? timeProvider = null, SubscriptionMode subscriptionMode = SubscriptionMode.Stream) 
        : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize,
        noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory,
        makeChannels, emptyChannelDelay, channelFailureDelay)
    {
        Labels = labels ?? new MapField<string, string>();
        MaxRequeueDelay = maxRequeueDelay ?? TimeSpan.FromSeconds(600);;
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
    }
}

/// <summary>
/// Represents a Google Cloud Pub/Sub subscription specific to a request type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the request message expected to be consumed from this subscription.</typeparam>
public class GcpSubscription<T> : GcpSubscription
    where T : class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GcpSubscription{T}"/> class, inheriting parameters from the base class 
    /// but strongly typing the expected request type to <typeparamref name="T"/>.
    /// </summary>
    /// <param name="subscriptionName">The name of the Pub/Sub subscription.</param>
    /// <param name="channelName">The name of the Brighter channel.</param>
    /// <param name="routingKey">The topic name/routing key.</param>
    /// <param name="getRequestType">A function to dynamically determine the request type from the message (optional).</param>
    /// <param name="bufferSize">The number of messages to buffer per performer (for internal channel control).</param>
    /// <param name="noOfPerformers">The number of concurrent performers (threads/tasks) processing messages.</param>
    /// <param name="timeOut">The timeout for a single receive operation.</param>
    /// <param name="requeueCount">The number of times a message should be redelivered before being considered unacceptable.</param>
    /// <param name="requeueDelay">The initial delay for requeueing a message after rejection.</param>
    /// <param name="unacceptableMessageLimit">The limit for unacceptable messages before the pump shuts down.</param>
    /// <param name="messagePumpType">The type of message pump to use.</param>
    /// <param name="channelFactory">A custom channel factory (optional).</param>
    /// <param name="makeChannels">Defines whether to create the channel if missing.</param>
    /// <param name="emptyChannelDelay">The delay when the channel is empty.</param>
    /// <param name="channelFailureDelay">The delay when the channel fails.</param>
    /// <param name="projectId">The Google Cloud Project ID where the subscription resides.</param>
    /// <param name="topicAttributes">Attributes to be applied to the associated Pub/Sub Topic upon creation.</param>
    /// <param name="ackDeadlineSeconds">The acknowledgment deadline in seconds (10 to 600).</param>
    /// <param name="retainAckedMessages">Whether to retain acknowledged messages.</param>
    /// <param name="messageRetentionDuration">The duration for message retention.</param>
    /// <param name="labels">Key-value labels to apply to the subscription resource.</param>
    /// <param name="enableMessageOrdering">Whether to enable message ordering.</param>
    /// <param name="enableExactlyOnceDelivery">Whether to enable exactly-once delivery.</param>
    /// <param name="storage">Configuration for exporting acknowledged messages to Google Cloud Storage.</param>
    /// <param name="expirationPolicy">Policy governing automatic subscription deletion based on inactivity.</param>
    /// <param name="deadLetter">Policy for dead-lettering messages that fail delivery too many times.</param>
    /// <param name="maxRequeueDelay">The maximum delay for exponential backoff retries.</param>
    /// <param name="timeProvider">The time provider used for time-related calculations.</param>
    /// <param name="subscriptionMode">The delivery mechanism to use (Stream or Pull).</param>
    public GcpSubscription(SubscriptionName subscriptionName, ChannelName channelName, RoutingKey routingKey, 
        Func<Message, Type>? getRequestType = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null,
        int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null, string? projectId = null, TopicAttributes? topicAttributes = null, int ackDeadlineSeconds = 30,
        bool retainAckedMessages = false, TimeSpan? messageRetentionDuration = null, MapField<string, string>? labels = null, 
        bool enableMessageOrdering = false, bool enableExactlyOnceDelivery = false, CloudStorageConfig? storage = null,
        ExpirationPolicy? expirationPolicy = null, DeadLetterPolicy? deadLetter = null, TimeSpan? maxRequeueDelay = null,
        TimeProvider? timeProvider = null, SubscriptionMode subscriptionMode = SubscriptionMode.Stream)
        : base(subscriptionName, channelName, routingKey, typeof(T), getRequestType, bufferSize,
            noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType,
            channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay, projectId, topicAttributes,
            ackDeadlineSeconds, retainAckedMessages, messageRetentionDuration, labels, enableMessageOrdering, 
            enableExactlyOnceDelivery, storage, expirationPolicy, deadLetter, maxRequeueDelay, timeProvider, subscriptionMode: subscriptionMode)
    {
    }
}
