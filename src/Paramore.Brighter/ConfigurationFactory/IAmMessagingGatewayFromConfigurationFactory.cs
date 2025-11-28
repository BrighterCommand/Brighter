using System.Collections.Generic;

namespace Paramore.Brighter.ConfigurationFactory;

/// <summary>
/// Factory interface for creating messaging gateway components from configuration.
/// Enables configuration-based initialization of messaging infrastructure (channels, consumers, producers)
/// for transport implementations like RabbitMQ, Kafka, AWS SNS/SQS, Azure Service Bus, etc.
/// </summary>
/// <remarks>
/// This interface provides a consistent pattern for configuring messaging gateways across different transport
/// providers while maintaining independence from specific configuration systems. Implementations should support
/// reading from Brighter-specific configuration sections (e.g., "Brighter:RabbitMQ"), .NET Aspire configuration,
/// and standard connection strings.
/// See ADR 0035 for details on Brighter's configuration support strategy.
/// </remarks>
public interface IAmMessagingGatewayFromConfigurationFactory
{
    /// <summary>
    /// Creates a gateway configuration from the provided configuration source.
    /// </summary>
    /// <param name="configuration">The <see cref="IAmAConfiguration"/> containing the messaging gateway settings.</param>
    /// <param name="name">The optional name for named configuration instances, allowing multiple configurations for the same provider.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses the provider's default section name.</param>
    /// <returns>An <see cref="IAmGatewayConfiguration"/> instance configured from the provided settings.</returns>
    /// <remarks>
    /// The gateway configuration contains transport-specific connection settings, credentials, and endpoint information.
    /// When <paramref name="name"/> is provided, the factory looks for configuration at "Brighter:{sectionName}:{name}".
    /// </remarks>
    IAmGatewayConfiguration CreateMessageGatewayConfigurationFactory(IAmAConfiguration configuration, string? name, string? sectionName);
    
    /// <summary>
    /// Creates a channel factory from the provided configuration source.
    /// </summary>
    /// <param name="configuration">The <see cref="IAmAConfiguration"/> containing the channel factory settings.</param>
    /// <param name="name">The optional name for named configuration instances, allowing multiple configurations for the same provider.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses the provider's default section name.</param>
    /// <returns>An <see cref="IAmAChannelFactory"/> instance that creates channels for message consumption.</returns>
    /// <remarks>
    /// The channel factory is responsible for creating channels that connect message consumers to the underlying
    /// transport. Configuration typically includes connection details, channel properties, and consumer behavior settings.
    /// </remarks>
    IAmAChannelFactory CreateChannelFactory(IAmAConfiguration configuration, string? name, string? sectionName);
    
    /// <summary>
    /// Creates a message consumer factory from the provided configuration source.
    /// </summary>
    /// <param name="configuration">The <see cref="IAmAConfiguration"/> containing the message consumer factory settings.</param>
    /// <param name="name">The optional name for named configuration instances, allowing multiple configurations for the same provider.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses the provider's default section name.</param>
    /// <returns>An <see cref="IAmAMessageConsumerFactory"/> instance that creates message consumers for receiving messages.</returns>
    /// <remarks>
    /// The message consumer factory creates consumers that receive messages from the transport. Configuration
    /// includes connection settings, subscription details, and consumer-specific options like prefetch count,
    /// acknowledgment mode, and retry policies.
    /// </remarks>
    IAmAMessageConsumerFactory CreateMessageConsumerFactory(IAmAConfiguration configuration, string? name, string? sectionName);
    
    /// <summary>
    /// Creates a producer registry factory from the provided configuration source.
    /// </summary>
    /// <param name="configuration">The <see cref="IAmAConfiguration"/> containing the producer registry factory settings.</param>
    /// <param name="name">The optional name for named configuration instances, allowing multiple configurations for the same provider.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses the provider's default section name.</param>
    /// <returns>An <see cref="IAmAProducerRegistryFactory"/> instance that creates producer registries for sending messages.</returns>
    /// <remarks>
    /// The producer registry factory creates registries that manage message producers (publishers) for sending
    /// messages to the transport. Configuration includes connection settings, publication details, and producer-specific
    /// options like confirmation mode, persistence settings, and routing strategies.
    /// </remarks>
    IAmAMessageProducerFactory CreateMessageProducerFactory(IAmAConfiguration configuration, string? name, string? sectionName);

    /// <summary>
    /// Creates a collection of subscriptions from the provided configuration source.
    /// </summary>
    /// <param name="configuration">The <see cref="IAmAConfiguration"/> containing the subscription settings.</param>
    /// <param name="name">The optional name for named configuration instances, allowing multiple configurations for the same provider.</param>
    /// <param name="sectionName">The optional override for the configuration section name. If null, uses the provider's default section name.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="Subscription"/> instances configured from the provided settings.</returns>
    /// <remarks>
    /// Subscriptions define the bindings between message handlers and messaging infrastructure (topics, queues, routing keys).
    /// Configuration typically includes subscription names, channel names, routing keys/patterns, message types,
    /// and consumer-specific settings like buffer size, unacceptable message limits, and requeue options.
    /// Multiple subscriptions can be defined in configuration to set up complex message routing scenarios.
    /// </remarks>
    IEnumerable<Subscription> CreateSubscriptions(IAmAConfiguration configuration, string? name, string? sectionName);
}
