#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion


using System;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

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
    /// Subscription is Session Enabled
    /// </summary>
    public bool RequireSession { get; set; } = false;

    /// <summary>
    /// Gets or sets the SQL filter to apply to the subscription.
    /// </summary>
    /// <value>The SQL filter expression, or an empty string to use the default rule.</value>
    public string SqlFilter { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to use a Service Bus queue instead of a topic.
    /// </summary>
    /// <value><see langword="true"/> to use a queue; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
    /// <remarks>
    /// When a consumer creates a queue, the delivery count, expiration, lock duration, lifetime,
    /// idle timeout and session settings are applied. Existing queues are not reconfigured.
    /// Producers create queues with broker defaults. Provision session-enabled queues before
    /// producers start, or let the configured consumer create them first: Azure Service Bus
    /// does not allow the session requirement to be changed after queue creation.
    /// </remarks>
    public bool UseServiceBusQueue { get; set; } = false;
}
