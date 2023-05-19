using System;
using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusSubscriptionConfiguration
    {
        /// <summary>
        /// The Maximum amount of times that a Message can be delivered before it is dead Lettered
        /// </summary>
        public int MaxDeliveryCount { get; set; } = 5;
        
        /// <summary>
        /// Dead letter a message when it expires
        /// </summary>
        public bool DeadLetteringOnMessageExpiration { get; set; } = true;
        
        /// <summary>
        /// How long message locks are held for
        /// </summary>
        public TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(1);
        
        /// <summary>
        /// How long messages sit in the queue before they expire
        /// </summary>
        public TimeSpan DefaultMessageTimeToLive { get; set; } = TimeSpan.FromDays(3);

        /// <summary>
        /// How long a queue is idle for before being deleted.
        /// Default is TimeSpan.MaxValue.
        /// </summary>
        public TimeSpan QueueIdleBeforeDelete { get; set; } = TimeSpan.MaxValue;
        
        /// <summary>
        /// A Sql Filter to apply to the subscription
        /// </summary>
        public string SqlFilter = String.Empty;
    }
}
