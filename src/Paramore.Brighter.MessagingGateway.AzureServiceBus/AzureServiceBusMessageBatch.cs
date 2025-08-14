using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <inheritdoc/>
public class AzureServiceBusMessageBatch : IAmAMessageBatch<ServiceBusMessageBatch>
{
    private readonly List<Id> _ids = new List<Id>();

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureServiceBusMessageBatch"/> class with a collection of messages. 
    /// </summary>
    private AzureServiceBusMessageBatch(ServiceBusMessageBatch messageBatch, RoutingKey routingKey)
    {
        if (RoutingKey.IsNullOrEmpty(routingKey))
            throw new ArgumentNullException(nameof(routingKey));

        Content = messageBatch;
        RoutingKey = routingKey;
    }

    /// <inheritdoc/>
    public IEnumerable<Id> Ids() => _ids;

    /// <inheritdoc/>
    public ServiceBusMessageBatch Content { get; }

    /// <inheritdoc/>
    public RoutingKey RoutingKey { get; }

    public bool IsEmpty => Content.Count == 0;

    public bool TryAddMessage(Message message)
    {
        if (!RoutingKey.Equals(message.Header.Topic))
            throw new InvalidOperationException(
                $"AzureServiceBusMessageBatch can only contain messages for the same routing key {RoutingKey}, {message.Header.Topic}");

        if (!Content.TryAddMessage(AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message)))
            return false;

        _ids.Add(message.Id);

        return true;
    }

    public static async Task<AzureServiceBusMessageBatch> CreateBatch(IServiceBusSenderWrapper sender, RoutingKey routingKey,
        CancellationToken cancellationToken)
        => new(await sender.CreateMessageBatchAsync(cancellationToken), routingKey);
}
