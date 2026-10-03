#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

public class InMemoryServiceBusAdministrationClient : ServiceBusAdministrationClient
{
    public Dictionary<string, CreateQueueOptions> Queues { get; } = new();

    public override Task<Response<bool>> QueueExistsAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(Response.FromValue(Queues.ContainsKey(name), null!));

    public override Task<Response<QueueProperties>> CreateQueueAsync(CreateQueueOptions options,
        CancellationToken cancellationToken = default)
    {
        Queues.Add(options.Name, options);
        var properties = ServiceBusModelFactory.QueueProperties(
            name: options.Name,
            lockDuration: options.LockDuration,
            maxSizeInMegabytes: options.MaxSizeInMegabytes,
            requiresSession: options.RequiresSession,
            defaultMessageTimeToLive: options.DefaultMessageTimeToLive,
            autoDeleteOnIdle: options.AutoDeleteOnIdle,
            deadLetteringOnMessageExpiration: options.DeadLetteringOnMessageExpiration,
            duplicateDetectionHistoryTimeWindow: options.DuplicateDetectionHistoryTimeWindow,
            maxDeliveryCount: options.MaxDeliveryCount,
            userMetadata: options.UserMetadata ?? string.Empty);
        return Task.FromResult(Response.FromValue(properties, null!));
    }
}
