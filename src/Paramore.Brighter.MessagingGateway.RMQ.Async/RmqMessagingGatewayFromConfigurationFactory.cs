using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.ConfigurationFactory;
using RabbitMQ.Client;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

/// <summary>
/// Factory for creating RabbitMQ messaging gateway components from configuration.
/// Enables configuration-based initialization of RabbitMQ channels, consumers, producers, and subscriptions
/// from configuration sources including Brighter-specific sections, .NET Aspire, and connection strings.
/// </summary>
/// <remarks>
/// This factory implements <see cref="IAmMessagingGatewayFromConfigurationFactory"/> to provide RabbitMQ-specific
/// configuration support. It follows a multi-source configuration strategy with the following precedence:
/// <list type="number">
/// <item><description>Brighter:RabbitMQ:{name} - Highest priority, Brighter-specific configuration</description></item>
/// <item><description>Aspire:RabbitMQ:Client - .NET Aspire orchestration configuration</description></item>
/// <item><description>ConnectionStrings:{name} - Standard .NET connection strings</description></item>
/// </list>
/// <para>
/// Configuration supports both default (unnamed) and named instances, allowing multiple RabbitMQ connections
/// within the same application. Named instances enable scenarios like separate connections for analytics,
/// reporting, or different message brokers.
/// </para>
/// <para>
/// Default configuration section: "Brighter:RabbitMQ"
/// </para>
/// See ADR 0035 for details on Brighter's configuration support strategy.
/// </remarks>
public class RmqMessagingGatewayFromConfigurationFactory : IAmMessagingGatewayFromConfigurationFactory
{
    private const string BrighterSection = "Brighter:RabbitMQ";
    private const string AspireSection = "Aspire:RabbitMQ:Client";

    private const string AspireConnection = "ConnectionString";

    /// <summary>
    /// Creates a RabbitMQ gateway configuration from the provided configuration source.
    /// </summary>
    /// <param name="configuration">The <see cref="IAmAConfiguration"/> containing the RabbitMQ gateway settings.</param>
    /// <param name="name">The optional name for named configuration instances, allowing multiple RabbitMQ connections.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses <see cref="BrighterSection"/>.</param>
    /// <returns>A <see cref="RmqMessagingGatewayConnection"/> configured from the provided settings.</returns>
    /// <remarks>
    /// This method resolves RabbitMQ connection configuration from multiple sources with the precedence order
    /// documented in <see cref="GetRabbitMqConfiguration"/>. It returns a gateway connection that can be used
    /// to create producers and consumers.
    /// </remarks>
    public IAmGatewayConfiguration CreateMessageGatewayConfigurationFactory(IAmAConfiguration configuration, string? name,
        string? sectionName)
    {
        var rabbitMqConfiguration = GetRabbitMqConfiguration(configuration, name ?? string.Empty, sectionName);
        return rabbitMqConfiguration.Connection.ToMessagingGatewayConnection();
    }

    /// <summary>
    /// Creates a RabbitMQ channel factory from the provided configuration source.
    /// </summary>
    /// <param name="configuration">The <see cref="IAmAConfiguration"/> containing the RabbitMQ channel factory settings.</param>
    /// <param name="name">The optional name for named configuration instances, allowing multiple RabbitMQ connections.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses <see cref="BrighterSection"/>.</param>
    /// <returns>A <see cref="ChannelFactory"/> that creates RabbitMQ channels for message consumption.</returns>
    /// <remarks>
    /// The channel factory creates channels that connect message consumers to RabbitMQ queues.
    /// Channels are configured with the connection settings resolved from the configuration.
    /// </remarks>
    public IAmAChannelFactory CreateChannelFactory(IAmAConfiguration configuration, string? name, string? sectionName)
    {
        var rabbitMqConfiguration = GetRabbitMqConfiguration(configuration, name ?? string.Empty, sectionName);
        var connection = rabbitMqConfiguration.Connection.ToMessagingGatewayConnection();
        return new ChannelFactory(new RmqMessageConsumerFactory(connection));
    }

    /// <summary>
    /// Creates a RabbitMQ message consumer factory from the provided configuration source.
    /// </summary>
    /// <param name="configuration">The <see cref="IAmAConfiguration"/> containing the RabbitMQ consumer factory settings.</param>
    /// <param name="name">The optional name for named configuration instances, allowing multiple RabbitMQ connections.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses <see cref="BrighterSection"/>.</param>
    /// <returns>A <see cref="RmqMessageConsumerFactory"/> that creates RabbitMQ message consumers.</returns>
    /// <remarks>
    /// The message consumer factory creates consumers that receive messages from RabbitMQ queues.
    /// Consumers are configured with connection settings, prefetch counts, and acknowledgment modes
    /// resolved from the configuration.
    /// </remarks>
    public IAmAMessageConsumerFactory CreateMessageConsumerFactory(IAmAConfiguration configuration, 
        string? name,
        string? sectionName)
    {
        var rabbitMqConfiguration = GetRabbitMqConfiguration(configuration, name ?? string.Empty, sectionName);
        var connection = rabbitMqConfiguration.Connection.ToMessagingGatewayConnection();
        return new RmqMessageConsumerFactory(connection);
    }

    public IAmAMessageProducerFactory CreateMessageProducerFactory(IAmAConfiguration configuration,
        string? name,
        string? sectionName)
    {
        var rabbitMqConfiguration = GetRabbitMqConfiguration(configuration, name ?? string.Empty, sectionName);
        var connection = rabbitMqConfiguration.Connection.ToMessagingGatewayConnection();

        return new RmqMessageProducerFactory(connection,
            rabbitMqConfiguration.Publications.Select(x => x.ToPublication()));
    }

    /// <summary>
    /// Creates a collection of RabbitMQ subscriptions from the provided configuration source.
    /// </summary>
    /// <param name="configuration">The <see cref="IAmAConfiguration"/> containing the RabbitMQ subscription settings.</param>
    /// <param name="name">The optional name for named configuration instances, allowing multiple RabbitMQ connections.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses <see cref="BrighterSection"/>.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="RmqSubscription"/> instances configured from the provided settings.</returns>
    /// <remarks>
    /// This method converts all subscription configurations into runtime <see cref="RmqSubscription"/> objects
    /// used by Brighter's message pump. Each subscription defines a queue binding with routing keys, dead letter
    /// configuration, and consumer behavior settings. The channel factory is shared across all subscriptions
    /// using the same connection configuration.
    /// </remarks>
    public IEnumerable<Subscription> CreateSubscriptions(IAmAConfiguration configuration, string? name, string? sectionName)
    {
        var rabbitMqConfiguration = GetRabbitMqConfiguration(configuration, name ?? string.Empty, sectionName);
        var connection = rabbitMqConfiguration.Connection.ToMessagingGatewayConnection();
        var factory = new ChannelFactory(new RmqMessageConsumerFactory(connection));
        return rabbitMqConfiguration
            .Subscriptions
            .Select(x => x.ToSubscription(factory));
    }


    /// <summary>
    /// Retrieves RabbitMQ configuration from multiple sources with fallback behavior.
    /// </summary>
    /// <param name="configuration">The <see cref="IAmAConfiguration"/> containing the configuration sources.</param>
    /// <param name="name">The name for named configuration instances. Use empty string for default configuration.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses <see cref="BrighterSection"/>.</param>
    /// <returns>A <see cref="RabbitMqConfiguration"/> instance with settings from all applicable configuration sources.</returns>
    /// <remarks>
    /// This method implements a multi-source configuration strategy with the following resolution order:
    /// <list type="number">
    /// <item><description>Brighter:RabbitMQ - Base Brighter-specific configuration</description></item>
    /// <item><description>Brighter:RabbitMQ:{name} - Named instance overrides (if name is provided)</description></item>
    /// <item><description>Aspire:RabbitMQ:Client:ConnectionString - .NET Aspire connection string</description></item>
    /// <item><description>Aspire:RabbitMQ:Client:{name}:ConnectionString - Named Aspire connection string</description></item>
    /// <item><description>ConnectionStrings:{name} - Standard .NET connection string</description></item>
    /// </list>
    /// <para>
    /// Later sources override earlier ones. This enables flexible configuration management where base settings
    /// can be defined in Brighter configuration and environment-specific connection strings can override them
    /// through Aspire or standard connection strings.
    /// </para>
    /// </remarks>
    private static RabbitMqConfiguration GetRabbitMqConfiguration(IAmAConfiguration configuration,
        string name,
        string? sectionName)
    {
        if (string.IsNullOrEmpty(sectionName))
        {
            sectionName = BrighterSection;
        }
        
        var configurationSection = configuration.GetSection(sectionName!); 
        var namedConfigurationSection = configurationSection.GetSection(name);
        
        var rabbitMqConfiguration = new RabbitMqConfiguration();
        configurationSection.Bind(rabbitMqConfiguration);
        namedConfigurationSection.Bind(rabbitMqConfiguration);
        
        var aspireConfiguration = configuration.GetSection(AspireSection);
        var namedAspireConfiguration = aspireConfiguration.GetSection(name);
        
        var connection = aspireConfiguration.GetSection(AspireConnection).Get<string>();
        if (!string.IsNullOrEmpty(connection))
        {
            rabbitMqConfiguration.Connection.AmpqUri ??= new AmqpUriSpecificationConfiguration();
            rabbitMqConfiguration.Connection.AmpqUri.Uri = connection!;
        }
        
        connection = namedAspireConfiguration.GetSection(AspireConnection).Get<string>();
        if (!string.IsNullOrEmpty(connection))
        {
            rabbitMqConfiguration.Connection.AmpqUri ??= new AmqpUriSpecificationConfiguration();
            rabbitMqConfiguration.Connection.AmpqUri.Uri = connection!;
        }

        connection = configuration.GetConnectionString(name);
        if (!string.IsNullOrEmpty(connection))
        {
            rabbitMqConfiguration.Connection.AmpqUri ??= new AmqpUriSpecificationConfiguration();
            rabbitMqConfiguration.Connection.AmpqUri.Uri = connection!;
        }

        return rabbitMqConfiguration;
    }
    
    /// <summary>
    /// Root configuration class for RabbitMQ that binds to configuration sections.
    /// Contains connection details, subscriptions, and publications for RabbitMQ messaging.
    /// </summary>
    /// <remarks>
    /// This class represents the complete RabbitMQ configuration structure that maps to JSON configuration.
    /// Example configuration structure:
    /// <code>
    /// {
    ///   "Brighter": {
    ///     "RabbitMQ": {
    ///       "Connection": { ... },
    ///       "Subscriptions": [ ... ],
    ///       "Publications": [ ... ]
    ///     }
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public class RabbitMqConfiguration
    {
        /// <summary>
        /// Gets or sets the RabbitMQ gateway connection configuration.
        /// </summary>
        /// <value>A <see cref="GatewayConnection"/> containing connection details for RabbitMQ.</value>
        public GatewayConnection Connection { get; set; } = null!;
        
        /// <summary>
        /// Gets or sets the list of RabbitMQ subscriptions.
        /// </summary>
        /// <value>A list of <see cref="RabbitMqSubscriptionConfiguration"/> defining how to consume messages from RabbitMQ.</value>
        public List<RabbitMqSubscriptionConfiguration> Subscriptions { get; set; } = [];
        
        /// <summary>
        /// Gets or sets the list of RabbitMQ publications.
        /// </summary>
        /// <value>A list of <see cref="RabbitMqPublicationConfiguration"/> defining how to publish messages to RabbitMQ.</value>
        public List<RabbitMqPublicationConfiguration> Publications { get; set; } = [];
    } 
    
    /// <summary>
    /// Configuration class for RabbitMQ gateway connection details.
    /// Contains connection URIs, exchange information, heartbeat settings, and message persistence options.
    /// </summary>
    /// <remarks>
    /// This class supports AMQP URI format for connection details. The configuration can include primary
    /// exchanges and dead letter exchanges for advanced routing scenarios.
    /// Connection settings include heartbeat intervals and continuation timeouts for robust connection management.
    /// </remarks>
    public class GatewayConnection 
    {
        /// <summary>
        /// Gets or sets the name identifier for this RabbitMQ connection.
        /// </summary>
        /// <value>The connection name as a <see cref="string"/>. Default is the machine name from <see cref="Environment.MachineName"/>.</value>
        /// <remarks>
        /// This name is used for connection identification in RabbitMQ management UI and logs.
        /// It helps distinguish between multiple connections from different machines or applications.
        /// </remarks>
        public string Name { get; set; } = Environment.MachineName;
        
        /// <summary>
        /// Gets or sets the AMQP URI specification for connecting to RabbitMQ.
        /// </summary>
        /// <value>An <see cref="AmqpUriSpecificationConfiguration"/> containing the connection URI, or null if not specified.</value>
        /// <remarks>
        /// AMQP URI format supports both amqp:// (plain) and amqps:// (TLS) protocols.
        /// Example: "amqp://guest:guest@localhost:5672/" or "amqps://user:pass@host:5671/vhost"
        /// This configuration can be provided directly or resolved from connection strings or Aspire configuration.
        /// </remarks>
        public AmqpUriSpecificationConfiguration? AmpqUri { get; set; } = null;
        
        /// <summary>
        /// Gets or sets the primary exchange configuration for message routing.
        /// </summary>
        /// <value>An <see cref="ExchangeConfiguration"/> defining the exchange settings, or null for default exchange.</value>
        /// <remarks>
        /// The exchange is the routing mechanism in RabbitMQ that receives messages from publishers and routes
        /// them to queues based on routing rules (direct, topic, fanout, headers).
        /// If null, the default exchange (empty string) will be used.
        /// </remarks>
        public ExchangeConfiguration? Exchange { get; set; }
        
        /// <summary>
        /// Gets or sets the dead letter exchange configuration for handling rejected or expired messages.
        /// </summary>
        /// <value>An <see cref="ExchangeConfiguration"/> for the dead letter exchange, or null if not using dead lettering.</value>
        /// <remarks>
        /// Dead letter exchanges receive messages that are rejected, expired, or exceed queue length limits.
        /// This enables building robust error handling and poison message management strategies.
        /// Configure dead letter queues bound to this exchange to capture and analyze failed messages.
        /// </remarks>
        public ExchangeConfiguration? DeadLetterExchange { get; set; }
        
        /// <summary>
        /// Gets or sets the heartbeat interval in seconds for detecting broken connections.
        /// </summary>
        /// <value>The heartbeat interval as a <see cref="ushort"/>. Default is 20 seconds.</value>
        /// <remarks>
        /// RabbitMQ uses heartbeats to detect dead TCP connections. Both client and server send heartbeat frames
        /// at intervals. If no frames are received within twice the heartbeat interval, the connection is considered dead.
        /// Set to 0 to disable heartbeats (not recommended for production).
        /// </remarks>
        public ushort Heartbeat { get; set; } = 20;
        
        /// <summary>
        /// Gets or sets whether messages should be persisted to disk.
        /// </summary>
        /// <value><c>true</c> to persist messages; otherwise, <c>false</c>. Default is <c>false</c>.</value>
        /// <remarks>
        /// When enabled, messages are marked as persistent (delivery mode 2) and written to disk.
        /// This ensures messages survive broker restarts but impacts performance.
        /// Use persistent messages for critical data that cannot be lost.
        /// Note: Queues must also be declared as durable for full persistence.
        /// </remarks>
        public bool PersistMessages { get; set; }
        
        /// <summary>
        /// Gets or sets the timeout in seconds for continuation operations.
        /// </summary>
        /// <value>The continuation timeout as a <see cref="ushort"/>. Default is 20 seconds.</value>
        /// <remarks>
        /// This timeout applies to asynchronous continuation operations in the RabbitMQ client.
        /// It determines how long to wait for protocol handshakes and channel operations to complete.
        /// </remarks>
        public ushort ContinuationTimeout { get; set; } = 20;
        
        /// <summary>
        /// Converts this configuration to a <see cref="RmqMessagingGatewayConnection"/> for runtime use.
        /// </summary>
        /// <returns>A <see cref="RmqMessagingGatewayConnection"/> instance configured from this configuration.</returns>
        /// <remarks>
        /// This method transforms the configuration-friendly representation into the runtime connection object
        /// used by RabbitMQ messaging gateway. It handles conversion of nullable properties and creates
        /// appropriate default values where needed.
        /// </remarks>
        public RmqMessagingGatewayConnection ToMessagingGatewayConnection()
        {
            return new RmqMessagingGatewayConnection
            {
                Name = Name,
                AmpqUri = AmpqUri != null ? new AmqpUriSpecification(
                    uri: new Uri(AmpqUri.Uri, UriKind.Absolute),
                    connectionRetryCount: AmpqUri.ConnectionRetryCount,
                    retryWaitInMilliseconds: AmpqUri.RetryWaitInMilliseconds,
                    circuitBreakTimeInMilliseconds: AmpqUri.CircuitBreakTimeInMilliseconds) : null,
                Exchange = Exchange != null ? new Exchange(
                    name: Exchange.Name,
                    type: Exchange.Type,
                    durable: Exchange.Durable, 
                    supportDelay: Exchange.SupportDelay) : null,
                DeadLetterExchange = DeadLetterExchange != null ? new Exchange(
                    name: DeadLetterExchange.Name,
                    type: DeadLetterExchange.Type,
                    durable: DeadLetterExchange.Durable, 
                    supportDelay: DeadLetterExchange.SupportDelay) : null,
                Heartbeat = Heartbeat,
                PersistMessages = PersistMessages,
                ContinuationTimeout = ContinuationTimeout,
            };
        }
    }

    /// <summary>
    /// Configuration class for AMQP URI specification including connection resilience settings.
    /// </summary>
    /// <remarks>
    /// This class contains the AMQP URI and parameters for connection retry logic and circuit breaker patterns
    /// to handle transient connection failures gracefully.
    /// </remarks>
    public class AmqpUriSpecificationConfiguration
    {
        /// <summary>
        /// Gets or sets the AMQP URI string for connecting to RabbitMQ.
        /// </summary>
        /// <value>The AMQP URI as a <see cref="string"/>. Default is an empty string.</value>
        /// <remarks>
        /// AMQP URI format: amqp://username:password@host:port/virtualhost or amqps:// for TLS.
        /// Example: "amqp://guest:guest@localhost:5672/" or "amqps://user:pass@broker.example.com:5671/production"
        /// </remarks>
        public string Uri { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the number of retry attempts for connection failures.
        /// </summary>
        /// <value>The retry count as an <see cref="int"/>. Default is 3.</value>
        /// <remarks>
        /// When a connection attempt fails, Brighter will retry this many times before giving up.
        /// Each retry is delayed by <see cref="RetryWaitInMilliseconds"/>.
        /// </remarks>
        public int ConnectionRetryCount { get; set; } = 3;
        
        /// <summary>
        /// Gets or sets the delay in milliseconds between connection retry attempts.
        /// </summary>
        /// <value>The retry delay in milliseconds as an <see cref="int"/>. Default is 1000ms (1 second).</value>
        /// <remarks>
        /// This delay helps prevent overwhelming a recovering broker with rapid reconnection attempts.
        /// Consider using exponential backoff for more sophisticated retry strategies.
        /// </remarks>
        public int RetryWaitInMilliseconds { get; set; } = 1_000;
        
        /// <summary>
        /// Gets or sets the circuit breaker timeout in milliseconds.
        /// </summary>
        /// <value>The circuit breaker timeout in milliseconds as an <see cref="int"/>. Default is 60000ms (1 minute).</value>
        /// <remarks>
        /// When the circuit breaker opens due to repeated failures, it remains open for this duration
        /// before attempting to reconnect. This prevents continuous connection attempts to a failed broker
        /// and allows time for infrastructure recovery.
        /// </remarks>
        public int CircuitBreakTimeInMilliseconds { get; set; } = 60_000;
    }
    
    /// <summary>
    /// Configuration class for RabbitMQ exchange settings.
    /// </summary>
    /// <remarks>
    /// Exchanges are the message routing components in RabbitMQ that receive messages from publishers
    /// and route them to queues based on exchange type and routing keys.
    /// </remarks>
    public class ExchangeConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the exchange.
        /// </summary>
        /// <value>The exchange name as a <see cref="string"/>. Default is an empty string (the default exchange).</value>
        /// <remarks>
        /// Exchange names must be unique within a virtual host. An empty string refers to the default exchange,
        /// which routes messages directly to queues by queue name.
        /// </remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the exchange.
        /// </summary>
        /// <value>The exchange type as a <see cref="string"/>. Default is <see cref="ExchangeType.Direct"/>.</value>
        /// <remarks>
        /// Common exchange types:
        /// <list type="bullet">
        /// <item><description><see cref="ExchangeType.Direct"/> - Routes to queues with exact routing key match</description></item>
        /// <item><description><see cref="ExchangeType.Topic"/> - Routes based on wildcard pattern matching (* and #)</description></item>
        /// <item><description><see cref="ExchangeType.Fanout"/> - Routes to all bound queues regardless of routing key</description></item>
        /// <item><description><see cref="ExchangeType.Headers"/> - Routes based on message header attributes</description></item>
        /// </list>
        /// </remarks>
        public string Type { get; set; } = ExchangeType.Direct;

        /// <summary>
        /// Gets or sets whether the exchange should survive broker restarts.
        /// </summary>
        /// <value><c>true</c> if the exchange is durable; otherwise, <c>false</c>. Default is <c>false</c>.</value>
        /// <remarks>
        /// Durable exchanges are persisted to disk and survive RabbitMQ broker restarts.
        /// Non-durable exchanges are transient and are lost when the broker restarts.
        /// For production systems, exchanges should typically be durable.
        /// </remarks>
        public bool Durable { get; set; }

        /// <summary>
        /// Gets or sets whether the exchange supports delayed message delivery.
        /// </summary>
        /// <value><c>true</c> if delayed delivery is supported; otherwise, <c>false</c>. Default is <c>false</c>.</value>
        /// <remarks>
        /// Delayed message delivery requires the RabbitMQ delayed message exchange plugin.
        /// When enabled, messages can be published with a delay header to defer delivery.
        /// This is useful for implementing scheduled tasks, retry delays, and time-based workflows.
        /// </remarks>
        public bool SupportDelay { get; set; }
    }
    
    /// <summary>
    /// Configuration class for RabbitMQ-specific subscription settings.
    /// Extends <see cref="SubscriptionConfiguration"/> with RabbitMQ queue features like dead lettering,
    /// high availability, durability, and message TTL.
    /// </summary>
    /// <remarks>
    /// This class provides configuration for RabbitMQ queues including advanced features like dead letter exchanges,
    /// quorum queues for high availability, and message time-to-live settings.
    /// Use <see cref="ToSubscription"/> to convert this configuration into a runtime <see cref="RmqSubscription"/>.
    /// </remarks>
    public class RabbitMqSubscriptionConfiguration : SubscriptionConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the dead letter queue channel.
        /// </summary>
        /// <value>The dead letter channel name as a <see cref="string"/>, or null if not using dead lettering.</value>
        /// <remarks>
        /// When a message is rejected or expires, it will be routed to this dead letter queue.
        /// The dead letter queue must be configured separately and bound to the dead letter exchange.
        /// </remarks>
        public string? DeadLetterChannel { get; set; }
        
        /// <summary>
        /// Gets or sets the routing key for dead letter messages.
        /// </summary>
        /// <value>The dead letter routing key as a <see cref="string"/>, or null to use the original routing key.</value>
        /// <remarks>
        /// This routing key is used when sending messages to the dead letter exchange.
        /// If null, the message's original routing key is preserved.
        /// Useful for routing different types of failures to different dead letter queues.
        /// </remarks>
        public string? DeadLetterRoutingKey { get; set; }
        
        /// <summary>
        /// Gets or sets whether the queue should be configured for high availability.
        /// </summary>
        /// <value><c>true</c> to enable high availability; otherwise, <c>false</c>. Default is <c>false</c>.</value>
        /// <remarks>
        /// High availability is typically achieved using quorum queues (RabbitMQ 3.8+) which replicate data
        /// across multiple nodes. This provides better data safety and availability but with some performance overhead.
        /// Consider using <see cref="QueueType.Quorum"/> for high availability queues.
        /// </remarks>
        public bool HighAvailability { get; set; }
        
        /// <summary>
        /// Gets or sets whether the queue should be durable (survive broker restarts).
        /// </summary>
        /// <value><c>true</c> if the queue is durable; otherwise, <c>false</c>. Default is <c>false</c>.</value>
        /// <remarks>
        /// Durable queues are persisted to disk and survive RabbitMQ broker restarts.
        /// For message durability, both the queue and messages must be marked as durable.
        /// Non-durable queues are transient and suitable for temporary or development scenarios.
        /// </remarks>
        public bool IsDurable { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum number of messages the queue can hold.
        /// </summary>
        /// <value>The maximum queue length as an <see cref="int"/>, or null for unlimited. Default is null.</value>
        /// <remarks>
        /// When the queue reaches this limit, new messages will be rejected or, if configured,
        /// routed to a dead letter exchange. This helps prevent memory exhaustion from unbounded queue growth.
        /// Use in conjunction with dead letter queues to avoid message loss.
        /// </remarks>
        public int? MaxQueueLength { get; set; }
        
        /// <summary>
        /// Gets or sets the message time-to-live (TTL) for messages in the queue.
        /// </summary>
        /// <value>The TTL as a <see cref="TimeSpan"/>, or null for no expiration. Default is null.</value>
        /// <remarks>
        /// Messages in the queue that exceed this TTL are automatically expired and, if configured,
        /// routed to a dead letter exchange. This prevents stale messages from accumulating.
        /// Individual messages can also have their own TTL which takes precedence.
        /// </remarks>
        public TimeSpan? Ttl { get; set; }
        
        /// <summary>
        /// Gets or sets the type of queue to create.
        /// </summary>
        /// <value>A <see cref="QueueType"/> value. Default is <see cref="QueueType.Classic"/>.</value>
        /// <remarks>
        /// Queue types include:
        /// <list type="bullet">
        /// <item><description><see cref="QueueType.Classic"/> - Traditional RabbitMQ queues</description></item>
        /// <item><description><see cref="QueueType.Quorum"/> - Replicated queues for high availability (RabbitMQ 3.8+)</description></item>
        /// </list>
        /// Quorum queues provide better data safety and availability at the cost of throughput.
        /// </remarks>
        public QueueType QueueType { get; set; } = QueueType.Classic;
        
        /// <summary>
        /// Converts this configuration to a <see cref="RmqSubscription"/> for runtime use.
        /// </summary>
        /// <param name="factory">The <see cref="IAmAChannelFactory"/> used to create channels for this subscription.</param>
        /// <returns>A <see cref="RmqSubscription"/> instance configured from this configuration.</returns>
        /// <remarks>
        /// This method transforms the configuration-friendly representation into the runtime subscription object
        /// used by Brighter's message pump. It resolves derived values (channel name, routing key) and creates
        /// the appropriate RabbitMQ-specific subscription with queue features like dead lettering and TTL.
        /// </remarks>
        public RmqSubscription ToSubscription(IAmAChannelFactory factory)
        {
            return new RmqSubscription(
                subscriptionName: new SubscriptionName(GetName()),
                channelName: new ChannelName(GetChannelName()),
                routingKey: new RoutingKey(GetRoutingKey()),
                requestType: GetRequestType(),
                bufferSize: BufferSize,
                noOfPerformers: NoOfPerformers,
                timeOut: TimeOut,
                requeueCount: RequeueCount,
                requeueDelay: RequeueDelay,
                unacceptableMessageLimit: UnacceptableMessageLimit,
                messagePumpType: MessagePumpType,
                makeChannels: MakeChannels,
                emptyChannelDelay: EmptyChannelDelay,
                channelFailureDelay: ChannelFailureDelay,
                channelFactory: factory,
                deadLetterChannelName: string.IsNullOrEmpty(DeadLetterChannel) ? null : new ChannelName(DeadLetterChannel!),
                deadLetterRoutingKey: string.IsNullOrEmpty(DeadLetterRoutingKey) ? null : new RoutingKey(DeadLetterRoutingKey!),
                highAvailability: HighAvailability,
                isDurable: IsDurable,
                ttl: Ttl,
                queueType: QueueType,
                maxQueueLength: MaxQueueLength);
        }
    }
    
    /// <summary>
    /// Configuration class for RabbitMQ-specific publication settings.
    /// Extends <see cref="PublicationConfiguration"/> with RabbitMQ publisher confirms timeout.
    /// </summary>
    /// <remarks>
    /// This class provides configuration for RabbitMQ message publishing including publisher confirms
    /// which provide reliable delivery guarantees by waiting for broker acknowledgment.
    /// Use <see cref="ToPublication"/> to convert this configuration into a runtime <see cref="RmqPublication"/>.
    /// </remarks>
    public class RabbitMqPublicationConfiguration : PublicationConfiguration
    {
        /// <summary>
        /// Gets or sets the timeout in milliseconds to wait for RabbitMQ publisher confirms.
        /// </summary>
        /// <value>The timeout in milliseconds as an <see cref="int"/>. Default is 500ms.</value>
        /// <remarks>
        /// Publisher confirms ensure messages are successfully received by the broker. When enabled,
        /// RabbitMQ sends an acknowledgment back to the publisher. This timeout controls how long to wait
        /// for that acknowledgment before considering the publish operation failed.
        /// <para>
        /// A longer timeout provides more resilience against transient network issues but increases
        /// publish latency. A shorter timeout improves throughput but may result in false failures.
        /// </para>
        /// <para>
        /// This setting only applies when publisher confirms are enabled on the connection.
        /// </para>
        /// </remarks>
        public int WaitForConfirmsTimeOutInMilliseconds { get; set; } = 500;

        /// <summary>
        /// Converts this configuration to a <see cref="RmqPublication"/> for runtime use.
        /// </summary>
        /// <returns>A <see cref="RmqPublication"/> instance configured from this configuration.</returns>
        /// <remarks>
        /// This method transforms the configuration-friendly representation into the runtime publication object
        /// used by Brighter's message producers. It converts string-based URIs and routing keys to their
        /// strongly-typed equivalents and applies CloudEvents properties.
        /// </remarks>
        public RmqPublication ToPublication()
        {
            Uri? dataschema = null;
            if (!string.IsNullOrEmpty(DataSchema))
            {
                dataschema = new Uri(DataSchema!, UriKind.RelativeOrAbsolute);
            }
            
            RoutingKey? topic = null;
            if (!string.IsNullOrEmpty(Topic))
            {
                topic = new RoutingKey(Topic!);
            }
            
            return new RmqPublication
            {
                DataSchema = dataschema,
                MakeChannels = MakeChannels,
                Source = new Uri(Source, UriKind.RelativeOrAbsolute),
                Subject = Subject,
                Topic = topic,
                Type = new CloudEventsType(Type),
                DefaultHeaders = DefaultHeaders,
                CloudEventsAdditionalProperties = CloudEventsAdditionalProperties,
                ReplyTo = ReplyTo,
                RequestType = GetRequestType(),
                WaitForConfirmsTimeOutInMilliseconds = WaitForConfirmsTimeOutInMilliseconds,
            };
        }
    }
}
