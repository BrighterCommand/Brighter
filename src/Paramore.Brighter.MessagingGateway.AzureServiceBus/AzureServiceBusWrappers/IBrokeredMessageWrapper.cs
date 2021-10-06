using System;
using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public interface IBrokeredMessageWrapper
    {
        byte[] MessageBodyValue { get; }
        
        IReadOnlyDictionary<string, object> UserProperties { get; }

        string LockToken { get; }

        Guid Id { get; }
        Guid CorrelationId { get; }

        string ContentType { get; }
    }
}
