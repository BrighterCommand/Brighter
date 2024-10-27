using System.Collections.Generic;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

public class BrokeredMessage : IBrokeredMessageWrapper
{
    public byte[] MessageBodyValue { get; init; }
    public IReadOnlyDictionary<string, object> ApplicationProperties { get; init; }
    public string LockToken { get;  init;}
    public string Id { get;  init;}
    public string CorrelationId { get;  init;}
    public string ContentType { get;  init;}
}
