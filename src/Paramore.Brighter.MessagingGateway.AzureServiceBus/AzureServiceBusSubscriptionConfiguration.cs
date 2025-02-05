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
    /// A Sql Filter to apply to the subscription
    /// </summary>
    public string SqlFilter = string.Empty;

    /// <summary>
    /// Use a Service Bus Queue instead of a Topic
    /// </summary>
    public bool UseServiceBusQueue = false;
}
