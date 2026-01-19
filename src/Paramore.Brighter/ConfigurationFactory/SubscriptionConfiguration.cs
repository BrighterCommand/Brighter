using System;
using System.Linq;
using System.Reflection;

namespace Paramore.Brighter.ConfigurationFactory;

/// <summary>
/// Base class for subscription configuration that can be loaded from configuration sources.
/// Provides properties for configuring how Brighter subscribes to and processes messages from a transport,
/// including channel behavior, message routing, and consumer performance settings.
/// </summary>
/// <remarks>
/// This abstract class is intended to be extended by transport-specific subscription configuration classes
/// (e.g., RabbitMQ, Kafka, AWS SQS) that bind to configuration sections. It provides common subscription
/// properties and helper methods for deriving runtime values from configuration.
/// See ADR 0035 for details on Brighter's configuration support strategy.
/// </remarks>
public abstract class SubscriptionConfiguration
{
    /// <summary>
    /// Gets or sets the size of the channel buffer for queuing messages.
    /// </summary>
    /// <value>The buffer size as an <see cref="int"/>. Default is 1.</value>
    /// <remarks>
    /// The buffer size determines how many messages can be queued in memory before the channel
    /// blocks. A larger buffer can improve throughput but increases memory usage.
    /// </remarks>
    public int BufferSize { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the name of the channel to create for this subscription.
    /// </summary>
    /// <value>The channel name as a <see cref="string"/>, or null to derive from <see cref="RequestType"/>.</value>
    /// <remarks>
    /// If not specified, the channel name will be derived from the <see cref="RequestType"/>.
    /// The channel name is used to identify the channel in the message pump and diagnostics.
    /// </remarks>
    public string? ChannelName { get; set; }
    
    /// <summary>
    /// Gets or sets the delay before retrying after a channel failure.
    /// </summary>
    /// <value>The delay as a <see cref="TimeSpan"/>, or null to use the default.</value>
    /// <remarks>
    /// This delay is applied when the channel encounters an error and needs to reconnect or recover.
    /// A longer delay can reduce load on failing infrastructure but increases recovery time.
    /// </remarks>
    public TimeSpan? ChannelFailureDelay { get; set; }
    
    /// <summary>
    /// Gets or sets the fully qualified type name of the request/message type for this subscription.
    /// </summary>
    /// <value>The fully qualified type name as a <see cref="string"/>, or null for untyped subscriptions.</value>
    /// <remarks>
    /// This should be the full type name including namespace (e.g., "MyApp.Commands.ProcessOrderCommand").
    /// The type is resolved at runtime using reflection across all loaded assemblies.
    /// Used as a fallback for <see cref="Name"/>, <see cref="ChannelName"/>, and <see cref="RoutingKey"/> if those are not specified.
    /// </remarks>
    public string? RequestType { get; set; }
    
    /// <summary>
    /// Gets or sets the delay to wait when the channel is empty before polling again.
    /// </summary>
    /// <value>The delay as a <see cref="TimeSpan"/>, or null to use the default.</value>
    /// <remarks>
    /// This delay reduces CPU usage when no messages are available by preventing tight polling loops.
    /// A shorter delay improves message processing latency but increases CPU usage.
    /// </remarks>
    public TimeSpan? EmptyChannelDelay { get; set; }
    
    /// <summary>
    /// Gets or sets the behavior for creating missing channels.
    /// </summary>
    /// <value>An <see cref="OnMissingChannel"/> value. Default is <see cref="OnMissingChannel.Assume"/>.</value>
    /// <remarks>
    /// Controls whether Brighter should create infrastructure (queues, topics, bindings) if it doesn't exist.
    /// Use <see cref="OnMissingChannel.Create"/> for development environments and <see cref="OnMissingChannel.Validate"/>
    /// or <see cref="OnMissingChannel.Assume"/> for production where infrastructure is managed separately.
    /// </remarks>
    public OnMissingChannel MakeChannels { get; set; } = OnMissingChannel.Assume;
    
    /// <summary>
    /// Gets or sets the name of the subscription.
    /// </summary>
    /// <value>The subscription name as a <see cref="string"/>, or null to derive from <see cref="RequestType"/> or generate a GUID.</value>
    /// <remarks>
    /// The subscription name is used to identify the subscription in configuration and diagnostics.
    /// If not specified, it will be derived from <see cref="RequestType"/> or a GUID will be generated.
    /// </remarks>
    public string? Name { get; set; }
    
    /// <summary>
    /// Gets or sets the number of concurrent message processors (performers) for this subscription.
    /// </summary>
    /// <value>The number of performers as an <see cref="int"/>. Default is 1.</value>
    /// <remarks>
    /// Each performer runs on a separate thread/task and processes messages concurrently.
    /// Increasing performers can improve throughput but requires thread-safe message handlers.
    /// </remarks>
    public int NoOfPerformers { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the maximum number of times to requeue a message before sending it to a dead letter queue.
    /// </summary>
    /// <value>The requeue count as an <see cref="int"/>. Default is -1 (unlimited).</value>
    /// <remarks>
    /// A value of -1 means messages will be requeued indefinitely. A value of 0 means no requeueing.
    /// Use this to prevent poison messages from blocking the queue indefinitely.
    /// </remarks>
    public int RequeueCount { get; set; } = -1;
    
    /// <summary>
    /// Gets or sets the delay before requeuing a message after a processing failure.
    /// </summary>
    /// <value>The delay as a <see cref="TimeSpan"/>, or null to requeue immediately.</value>
    /// <remarks>
    /// A delay can help with transient failures by giving downstream systems time to recover.
    /// </remarks>
    public TimeSpan? RequeueDelay { get; set;  }
    
    /// <summary>
    /// Gets or sets the routing key used to bind the subscription to topics/exchanges.
    /// </summary>
    /// <value>The routing key as a <see cref="string"/>. Default is an empty string.</value>
    /// <remarks>
    /// The routing key determines which messages are routed to this subscription. For topic-based
    /// transports like RabbitMQ, this supports wildcards (* and #). For direct routing, this should
    /// match the message's routing key exactly. If not specified, it will be derived from <see cref="RequestType"/>.
    /// </remarks>
    public string RoutingKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the type of message pump to use for this subscription.
    /// </summary>
    /// <value>A <see cref="MessagePumpType"/> value. Default is <see cref="MessagePumpType.Proactor"/>.</value>
    /// <remarks>
    /// <see cref="MessagePumpType.Proactor"/> uses asynchronous I/O and is suitable for I/O-bound workloads.
    /// <see cref="MessagePumpType.Reactor"/> uses synchronous I/O and may be better for CPU-bound workloads.
    /// </remarks>
    public MessagePumpType MessagePumpType { get; set; } = MessagePumpType.Proactor;
    
    /// <summary>
    /// Gets or sets the timeout for message processing.
    /// </summary>
    /// <value>The timeout as a <see cref="TimeSpan"/>, or null to use the default.</value>
    /// <remarks>
    /// If a message handler exceeds this timeout, the message pump may cancel the operation.
    /// Set this based on your handler's expected execution time plus a safety margin.
    /// </remarks>
    public TimeSpan? TimeOut { get; set;  }
    
    /// <summary>
    /// Gets or sets the maximum number of unacceptable messages before the channel stops processing.
    /// </summary>
    /// <value>The limit as an <see cref="int"/>. Default is 0 (unlimited).</value>
    /// <remarks>
    /// Unacceptable messages are those that cannot be deserialized or are malformed. A limit prevents
    /// a flood of bad messages from causing indefinite processing failures. A value of 0 means no limit.
    /// </remarks>
    public int UnacceptableMessageLimit { get; set; } = 0;
    
    /// <summary>
    /// Gets the subscription name, deriving it from <see cref="RequestType"/> or generating a GUID if necessary.
    /// </summary>
    /// <returns>The subscription name as a <see cref="string"/>.</returns>
    /// <remarks>
    /// Resolution order:
    /// 1. Returns <see cref="Name"/> if specified
    /// 2. Returns <see cref="RequestType"/> if specified
    /// 3. Generates a new GUID
    /// This method is typically called by derived classes when building subscription objects.
    /// </remarks>
    protected string GetName()
    {
        if (string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(RequestType))
        {
            return RequestType!;
        }

        if (string.IsNullOrEmpty(Name))
        {
            return Guid.NewGuid().ToString();
        }

        return Name!;
    }

    /// <summary>
    /// Gets the channel name, deriving it from <see cref="RequestType"/> if necessary.
    /// </summary>
    /// <returns>The channel name as a <see cref="string"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when both <see cref="ChannelName"/> and <see cref="RequestType"/> are null or empty.</exception>
    /// <remarks>
    /// Resolution order:
    /// 1. Returns <see cref="ChannelName"/> if specified
    /// 2. Returns <see cref="RequestType"/> if specified
    /// 3. Throws <see cref="ConfigurationException"/> if neither is available
    /// This method is typically called by derived classes when building subscription objects.
    /// </remarks>
    protected string GetChannelName()
    {
        if (string.IsNullOrEmpty(ChannelName) && !string.IsNullOrEmpty(RequestType))
        {
            return RequestType!;
        }
                
        if (string.IsNullOrEmpty(ChannelName))
        {
            throw new ConfigurationException(
                "Subscription configuration is missing ChannelName. Please specify either 'ChannelName' or 'RequestType' in your configuration. " +
                $"Current values - ChannelName: '{ChannelName ?? "null"}', RequestType: '{RequestType ?? "null"}'");
        }

        return ChannelName!;
    }

    /// <summary>
    /// Gets the routing key, deriving it from <see cref="RequestType"/> if necessary.
    /// </summary>
    /// <returns>The routing key as a <see cref="string"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when both <see cref="RoutingKey"/> and <see cref="RequestType"/> are null or empty.</exception>
    /// <remarks>
    /// Resolution order:
    /// 1. Returns <see cref="RoutingKey"/> if specified
    /// 2. Returns <see cref="RequestType"/> if specified
    /// 3. Throws <see cref="ConfigurationException"/> if neither is available
    /// This method is typically called by derived classes when building subscription objects.
    /// </remarks>
    protected string GetRoutingKey()
    {
        if (string.IsNullOrEmpty(RoutingKey) && !string.IsNullOrEmpty(RequestType))
        {
            return RequestType!;
        }
                
        if (string.IsNullOrEmpty(RoutingKey))
        {
            throw new ConfigurationException(
                "Subscription configuration is missing RoutingKey. Please specify either 'RoutingKey' or 'RequestType' in your configuration. " +
                $"Current values - RoutingKey: '{RoutingKey ?? "null"}', RequestType: '{RequestType ?? "null"}'");
        }

        return RoutingKey!;
    }

    /// <summary>
    /// Gets the request type by resolving the <see cref="RequestType"/> string to a <see cref="Type"/> using reflection.
    /// </summary>
    /// <returns>The resolved <see cref="Type"/>, or null if <see cref="RequestType"/> is not specified.</returns>
    /// <exception cref="ConfigurationException">Thrown when <see cref="RequestType"/> is specified but the type cannot be found in any loaded assembly.</exception>
    /// <remarks>
    /// This method searches all loaded assemblies in the current <see cref="AppDomain"/> for a concrete, non-abstract
    /// class matching the fully qualified type name specified in <see cref="RequestType"/>.
    /// The type must be a class (not an interface or struct) and must not be abstract.
    /// Example: "MyApp.Commands.ProcessOrderCommand, MyApp" or just "MyApp.Commands.ProcessOrderCommand" if the assembly is already loaded.
    /// </remarks>
    protected Type? GetRequestType()
    {
        if (string.IsNullOrEmpty(RequestType))
        {
            return null;
        }

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            var type = types.FirstOrDefault(x => x.FullName == RequestType);
            if (type != null && type.IsClass && !type.IsAbstract)
            {
                return type;
            }
        }

        throw new ConfigurationException(
            $"RequestType '{RequestType}' could not be resolved to a valid type. " +
            $"Ensure the type name is fully qualified (e.g., 'MyApp.Commands.ProcessOrderCommand'), " +
            $"the assembly containing the type is loaded, and the type is a concrete class (not abstract or an interface). " +
            $"Searched {assemblies.Length} loaded assemblies.");
    }
}
