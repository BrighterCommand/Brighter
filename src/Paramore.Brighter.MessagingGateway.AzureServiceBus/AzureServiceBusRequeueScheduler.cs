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

using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Schedules retries directly on a queue using Azure Service Bus native scheduling.
/// </summary>
/// <remarks>
/// The destination queue must already exist in the client's namespace. No administration requests
/// are made. A retry gets a distinct, deterministic delivery ID and retains its first ID in
/// <see cref="Message.OriginalMessageIdHeaderName"/>. The caller owns the client and settles the
/// original delivery after scheduling succeeds. Each sender is closed after use.
/// </remarks>
public sealed class AzureServiceBusRequeueScheduler : IAmAMessageRequeueSchedulerAsync, IAmAMessageRequeueSchedulerSync
{
    private readonly IServiceBusSenderProvider _senderProvider;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a native queue retry scheduler for the supplied client.</summary>
    /// <param name="clientProvider">The provider for the consuming broker's client.</param>
    /// <param name="timeProvider">The clock used to calculate delivery times; defaults to the system clock.</param>
    /// <param name="senderOptions">Options used when creating queue senders.</param>
    public AzureServiceBusRequeueScheduler(IServiceBusClientProvider clientProvider,
        TimeProvider? timeProvider = null, ServiceBusSenderOptions? senderOptions = null)
        : this(new ServiceBusSenderProvider(clientProvider, senderOptions), timeProvider ?? TimeProvider.System)
    {
    }

    internal AzureServiceBusRequeueScheduler(IServiceBusSenderProvider senderProvider, TimeProvider timeProvider)
    {
        _senderProvider = senderProvider;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task RequeueAsync(Message message, ChannelName destination, TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));
        if (destination is null || string.IsNullOrWhiteSpace(destination.Value)
            || Uri.TryCreate(destination.Value, UriKind.Absolute, out _))
            throw new ArgumentException("A queue name in the scheduler's namespace is required.", nameof(destination));
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "The retry delay cannot be negative.");
        cancellationToken.ThrowIfCancellationRequested();

        var retry = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message);
        retry.Body = new BinaryData(message.Body.Memory);
        var originalId = message.Header.Bag.TryGetValue(Message.OriginalMessageIdHeaderName, out var original)
            && !string.IsNullOrEmpty(original?.ToString()) ? original!.ToString()! : message.Id.Value;
        // A repeated scheduling call for the same delivery must not bypass broker duplicate detection.
        using var hash = SHA256.Create();
        var identity = JsonSerializer.Serialize(new[]
        {
            message.Id.Value, destination.Value,
            message.Header.HandledCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        retry.MessageId = "brighter-retry-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
        retry.ApplicationProperties[Message.OriginalMessageIdHeaderName] = originalId;
        retry.ApplicationProperties[ASBConstants.CloudEventsId] = retry.MessageId;

        var retrySender = _senderProvider.Get(destination.Value);
        try
        {
            await retrySender.ScheduleMessageAsync(retry, _timeProvider.GetUtcNow().Add(delay), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await retrySender.CloseAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Requeue(Message message, ChannelName destination, TimeSpan delay)
        => BrighterAsyncContext.Run(() => RequeueAsync(message, destination, delay));
}
