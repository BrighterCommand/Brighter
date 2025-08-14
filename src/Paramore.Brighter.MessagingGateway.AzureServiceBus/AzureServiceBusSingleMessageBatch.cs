using System;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <inheritdoc/>
public class AzureServiceBusSingleMessageBatch : IAmAMessageBatch<ServiceBusMessage>
{
    private readonly List<Id> _ids;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureServiceBusSingleMessageBatch"/> class with a single of messages. 
    /// </summary>
    private AzureServiceBusSingleMessageBatch(ServiceBusMessage content, RoutingKey routingKey, Id id)
    {
        if (RoutingKey.IsNullOrEmpty(routingKey))
            throw new ArgumentNullException(nameof(routingKey));

        Content = content;
        RoutingKey = routingKey;
        _ids = [id];
    }

    /// <inheritdoc/>
    public IEnumerable<Id> Ids() => _ids;

    /// <inheritdoc/>
    public ServiceBusMessage Content { get; }

    /// <inheritdoc/>
    public RoutingKey RoutingKey { get; }

    public static AzureServiceBusSingleMessageBatch CreateBatch(Message message)
        => new(AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message), message.Header.Topic, message.Id);
}
