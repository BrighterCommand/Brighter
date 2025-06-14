using System;
using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// Wrapper for a Brokered Message
    /// </summary>
    public interface IBrokeredMessageWrapper
    {
        /// <summary>
        /// Message Body.
        /// </summary>
        byte[]? MessageBodyValue { get; }

        /// <summary>
        /// Application Properties
        /// </summary>
        IReadOnlyDictionary<string, object> ApplicationProperties { get; }

        /// <summary>
        /// The Lock Token
        /// </summary>
        string LockToken { get; }

        /// <summary>
        /// The message Id.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// The Correlation Id.
        /// </summary>
        string CorrelationId { get; }

        /// <summary>
        /// The Mime Type.
        /// </summary>
        string ContentType { get; }
    }
}
