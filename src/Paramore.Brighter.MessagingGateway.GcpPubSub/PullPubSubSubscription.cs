using Google.Cloud.PubSub.V1;
using Google.Protobuf.Collections;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// Provides configuration specific to a Google Cloud Pub/Sub pull subscription for use with the Brighter framework.
/// </summary>
/// <remarks>
/// This class extends the base Brighter <see cref="Subscription"/> with properties that map directly to
/// Google Cloud Pub/Sub subscription settings. Configure these properties after creating an instance
/// to define the desired Pub/Sub behavior when the subscription is provisioned or used.
/// </remarks>
public class PullPubSubSubscription : Subscription
{
    /// <summary>
    /// Gets or sets the Google Cloud Project ID where the subscription resides or will be created.
    /// </summary>
    /// <value>The GCP Project ID.</value>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the attributes to be applied to the associated Pub/Sub Topic.
    /// </summary>
    /// <value>A <see cref="TopicAttributes"/> object containing topic configuration, typically used if Brighter needs to create the topic.</value>
    /// <remarks>This might contain settings like schema or KMS key name for the Topic itself.</remarks>
    public TopicAttributes? TopicAttributes { get; set; }

    /// <summary>
    /// Gets or sets the acknowledgment deadline for messages pulled from this subscription, in seconds.
    /// </summary>
    /// <value>The maximum time after receiving a message that the subscriber must acknowledge it before Pub/Sub attempts redelivery. Must be between 10 and 600 seconds.</value>
    /// <remarks>
    /// If not set, the Pub/Sub default (usually 30 seconds) applies when the subscription is created.
    /// </remarks>
    public int AckDeadlineSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether to retain acknowledged messages.
    /// </summary>
    /// <value><see langword="true"/> to retain acknowledged messages for the duration specified by <see cref="MessageRetentionDuration"/>; otherwise, <see langword="false"/>.</value>
    /// <remarks>Useful for features like 'seek to a time'. Defaults to <see langword="false"/>.</remarks>
    public bool RetainAckedMessages { get; set; }

    /// <summary>
    /// Gets or sets the duration for which Pub/Sub retains unacknowledged messages.
    /// </summary>
    /// <value>A <see cref="TimeSpan"/> representing the retention duration. Can range from 10 minutes to 7 days.</value>
    /// <remarks>
    /// If <see cref="RetainAckedMessages"/> is true, acknowledged messages are also retained for this duration.
    /// If not set, the Pub/Sub default (usually 7 days) applies when the subscription is created.
    /// </remarks>
    public TimeSpan? MessageRetentionDuration { get; set; }

    /// <summary>
    /// Gets or sets the collection of labels to apply to the Pub/Sub subscription resource.
    /// </summary>
    /// <value>A <see cref="MapField{TKey, TValue}"/> containing key-value string pairs.</value>
    /// <remarks>Labels can be used for organizing and filtering GCP resources.</remarks>
    public MapField<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to enable message ordering.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if Pub/Sub should deliver messages with the same ordering key in the order they were published;
    /// otherwise, <see langword="false"/>.
    /// </value>
    /// <remarks>Requires the publisher to set ordering keys on messages. Defaults to <see langword="false"/>.</remarks>
    public bool EnableMessageOrdering { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable exactly-once delivery semantics for this subscription.
    /// </summary>
    /// <value><see langword="true"/> to enable exactly-once delivery; otherwise, <see langword="false"/>.</value>
    /// <remarks>Provides stronger guarantees but may impact performance. Defaults to <see langword="false"/>.</remarks>
    public bool EnableExactlyOnceDelivery { get; set; }

    /// <summary>
    /// Gets or sets the configuration for exporting acknowledged messages to Google Cloud Storage.
    /// </summary>
    /// <value>A <see cref="CloudStorageConfig"/> object or <see langword="null"/> if not configured.</value>
    /// <remarks>This feature is typically used for loading data into BigQuery via subscriptions.</remarks>
    public CloudStorageConfig? Storage { get; set; }

    /// <summary>
    /// Gets or sets the policy governing the automatic deletion of the subscription based on inactivity.
    /// </summary>
    /// <value>
    /// An <see cref="ExpirationPolicy"/> object specifying the conditions under which the subscription
    /// expires (e.g., TTL after no usage), or <see langword="null"/> for no expiration.
    /// </value>
    public ExpirationPolicy? ExpirationPolicy { get; set; }

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
    public string? DeadLetterTopic { get; set; }

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
    public TimeSpan MaxRequeueDelay { get; set; } = TimeSpan.FromSeconds(600);

    /// <summary>
    /// Gets or sets the <see cref="TimeProvider"/> used for time-related operations (e.g., calculating expiration).
    /// </summary>
    /// <value>Defaults to <see cref="TimeProvider.System"/>.</value>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <inheritdoc />
    public override Type ChannelFactoryType => typeof(PullPubSubConsumerFactory);

    /// <summary>
    /// Initializes a new instance of the <see cref="PullPubSubSubscription"/> class, configuring base Brighter subscription properties.
    /// </summary>
    /// <param name="dataType">The type of the message payload.</param>
    /// <param name="name">The name of the subscription (logical name in Brighter).</param>
    /// <param name="channelName">The name of the channel (often corresponds to the physical Pub/Sub subscription ID).</param>
    /// <param name="routingKey">The routing key (often corresponds to the Pub/Sub topic ID).</param>
    /// <param name="bufferSize">The size of the buffer for messages read from the channel.</param>
    /// <param name="noOfPerformers">The number of parallel performers (message processors) to run.</param>
    /// <param name="timeOut">The timeout for channel operations (in milliseconds).</param>
    /// <param name="requeueCount">The number of times to requeue a message before discarding (handled by Brighter, distinct from Pub/Sub dead-lettering).</param>
    /// <param name="requeueDelay">The delay before requeuing a message (handled by Brighter).</param>
    /// <param name="unacceptableMessageLimit">The number of unacceptable messages allowed before stopping the performer.</param>
    /// <param name="messagePumpType">The type of message pump to use (use Unknown or specific if applicable).</param>
    /// <param name="channelFactory">An optional custom channel factory.</param>
    /// <param name="makeChannels">Specifies whether Brighter should attempt to create (provision) the channel if it's missing.</param>
    /// <param name="emptyChannelDelay">The delay before re-polling an empty channel.</param>
    /// <param name="channelFailureDelay">The delay before retrying after a channel failure.</param>
    /// <remarks>
    /// This constructor initializes the standard Brighter subscription properties.
    /// Google Cloud Pub/Sub specific settings (like <see cref="ProjectId"/>, <see cref="AckDeadlineSeconds"/>, <see cref="EnableMessageOrdering"/>, etc.)
    /// must be configured by setting the corresponding properties on the instance after creation. These settings are typically used
    /// during channel provisioning (<paramref name="makeChannels"/>  = <see cref="OnMissingChannel.Create"/> or <see cref="OnMissingChannel.Validate"/>)
    /// or by the <see cref="PullPubSubConsumerFactory"/> when creating the consumer.
    /// </remarks>
    public PullPubSubSubscription(Type dataType, SubscriptionName? name = null, ChannelName? channelName = null,
        RoutingKey? routingKey = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null,
        int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null) : base(dataType, name, channelName, routingKey, bufferSize,
        noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory,
        makeChannels, emptyChannelDelay, channelFailureDelay)
    {
    }
}

/// <summary>
/// Provides a generic version of <see cref="PullPubSubSubscription"/> for configuring a Google Cloud Pub/Sub
/// pull subscription for a specific message type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the message (<see cref="IRequest"/>) that this subscription handles. Must be a reference type.</typeparam>
/// <remarks>
/// This class simplifies subscription configuration by automatically setting the <c>DataType</c> property based on <typeparamref name="T"/>.
/// Like its base class, configure Google Cloud Pub/Sub specific properties on the instance after creation.
/// </remarks>
public class PullPubSubSubscription<T> : PullPubSubSubscription
    where T : class, IRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullPubSubSubscription{T}"/> class, configuring base Brighter subscription properties.
    /// </summary>
    /// <param name="name">The name of the subscription (logical name in Brighter).</param>
    /// <param name="channelName">The name of the channel (often corresponds to the physical Pub/Sub subscription ID).</param>
    /// <param name="routingKey">The routing key (often corresponds to the Pub/Sub topic ID).</param>
    /// <param name="bufferSize">The size of the buffer for messages read from the channel.</param>
    /// <param name="noOfPerformers">The number of parallel performers (message processors) to run.</param>
    /// <param name="timeOut">The timeout for channel operations (in milliseconds).</param>
    /// <param name="requeueCount">The number of times to requeue a message before discarding (handled by Brighter, distinct from Pub/Sub dead-lettering).</param>
    /// <param name="requeueDelay">The delay before requeuing a message (handled by Brighter).</param>
    /// <param name="unacceptableMessageLimit">The number of unacceptable messages allowed before stopping the performer.</param>
    /// <param name="messagePumpType">The type of message pump to use (use Unknown or specific if applicable).</param>
    /// <param name="channelFactory">An optional custom channel factory.</param>
    /// <param name="makeChannels">Specifies whether Brighter should attempt to create (provision) the channel if it's missing.</param>
    /// <param name="emptyChannelDelay">The delay before re-polling an empty channel.</param>
    /// <param name="channelFailureDelay">The delay before retrying after a channel failure.</param>
    /// <remarks>
    /// This constructor initializes the standard Brighter subscription properties.
    /// Google Cloud Pub/Sub specific settings (like <see cref="ProjectId"/>, <see cref="AckDeadlineSeconds"/>, <see cref="EnableMessageOrdering"/>, etc.)
    /// must be configured by setting the corresponding properties on the instance after creation. These settings are typically used
    /// during channel provisioning (<paramref name="makeChannels"/>  = <see cref="OnMissingChannel.Create"/> or <see cref="OnMissingChannel.Validate"/>)
    /// or by the <see cref="PullPubSubConsumerFactory"/> when creating the consumer.
    /// </remarks>
    public PullPubSubSubscription(SubscriptionName? name = null, ChannelName? channelName = null,
        RoutingKey? routingKey = null, int bufferSize = 1, int noOfPerformers = 1, TimeSpan? timeOut = null,
        int requeueCount = -1, TimeSpan? requeueDelay = null, int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown, IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create, TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null)
        : base(typeof(T), name, channelName, routingKey, bufferSize,
            noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType,
            channelFactory,
            makeChannels, emptyChannelDelay, channelFailureDelay)
    {
    }
}
