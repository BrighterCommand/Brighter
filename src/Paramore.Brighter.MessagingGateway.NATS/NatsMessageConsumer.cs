// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.NATS.Extensions;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Consumes messages from a core NATS subject on behalf of a <see cref="NatsSubscription"/>.
/// </summary>
/// <remarks>
/// Core NATS is at-most-once: there is no broker-side persistence or acknowledgement, so
/// <see cref="AcknowledgeAsync"/>, <see cref="NackAsync"/>, <see cref="RejectAsync"/>, and <see cref="PurgeAsync"/>
/// are no-ops. <see cref="RequeueAsync"/> republishes the message to its subject, which can yield duplicates;
/// use a <see cref="NatsStreamSubscription"/> when redelivery semantics are required. A requested requeue delay
/// is honored only when a scheduler is configured, as core NATS has no delayed delivery.
/// </remarks>
/// <param name="subscription">The <see cref="INatsSub{T}"/> to read messages from.</param>
/// <param name="client">The <see cref="INatsClient"/> used to republish on requeue.</param>
/// <param name="scheduler">An optional <see cref="IAmAMessageScheduler"/> used to honor delayed requeue; optional, defaults to <see langword="null"/>.</param>
public partial class NatsMessageConsumer(INatsSub<byte[]> subscription, INatsClient client, IAmAMessageScheduler? scheduler = null) : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<NatsMessageConsumer>();

    /// <summary>
    /// No-op: core NATS has no acknowledgement protocol.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to acknowledge.</param>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// No-op: core NATS has no rejection protocol; the message is simply dropped.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to reject.</param>
    /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message.</param>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// No-op: a core NATS subject cannot be purged.
    /// </summary>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    public Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Receives the next message from the subscription, waiting up to <paramref name="timeOut"/>.
    /// </summary>
    /// <param name="timeOut">How long to wait for a message; if omitted, waits until <paramref name="cancellationToken"/> is cancelled.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>An array with the received <see cref="Message"/>, or an empty <see cref="Message"/> if none arrived before the timeout.</returns>
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeOut.HasValue)
        {
            cts.CancelAfter(timeOut.Value);
        }

        try
        {
            var message = await subscription.Msgs.ReadAsync(cts.Token);
            return [message.ToMessage()];
        }
        catch (OperationCanceledException)
        {
            Log.NoMessagesAvailable(s_logger);
            return [new Message()];
        }
    }

    /// <summary>
    /// No-op: core NATS has no negative-acknowledgement protocol.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to nack.</param>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Requeues the message by republishing it to its subject, or via the configured scheduler when a delay is requested.
    /// </summary>
    /// <remarks>
    /// The republished message is a new delivery, so consumers can see duplicates and ordering is not preserved.
    /// Core NATS has no delayed delivery, so a non-zero <paramref name="delay"/> requires a scheduler.
    /// </remarks>
    /// <param name="message">The <see cref="Message"/> to requeue; must carry the original NATS message in its bag.</param>
    /// <param name="delay">How long to wait before the message is redelivered; <see langword="null"/> or <see cref="TimeSpan.Zero"/> republishes immediately.</param>
    /// <param name="cancellationToken">Cancels the republish.</param>
    /// <returns><see langword="true"/> if the message was requeued or scheduled; <see langword="false"/> if it did not carry a NATS message.</returns>
    /// <exception cref="ConfigurationException">Thrown when a non-zero delay is requested but no scheduler is configured.</exception>
    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        delay ??= TimeSpan.Zero;
        if (delay > TimeSpan.Zero)
        {
            if (scheduler is IAmAMessageSchedulerAsync asyncScheduler)
            {
                Log.SchedulingRequeue(s_logger, message.Id.Value, delay.Value);
                await asyncScheduler.ScheduleAsync(message, delay.Value, cancellationToken);
                return true;
            }

            if (scheduler is IAmAMessageSchedulerSync syncScheduler)
            {
                Log.SchedulingRequeue(s_logger, message.Id.Value, delay.Value);
                syncScheduler.Schedule(message, delay.Value);
                return true;
            }

            throw new ConfigurationException(
                $"NatsMessageConsumer: requeue delay of {delay} was requested but no scheduler is configured; configure a scheduler via the channel factory's Scheduler property.");
        }

        if (!message.Header.Bag.TryGetValue(HeadersName.NatsMessage, out var n) || n is not NatsMsg<byte[]> natsMsg)
        {
            Log.CannotRequeueMessage(s_logger, message.Id.Value);
            return false;
        }

        Log.RequeueingMessage(s_logger, message.Id.Value, message.Header.Topic.Value);
        await client.PublishAsync(message.Header.Topic.Value,
            natsMsg.Data,
            natsMsg.Headers,
            natsMsg.ReplyTo, cancellationToken: cancellationToken);

        return true;
    }

    /// <inheritdoc cref="AcknowledgeAsync"/>
    public void Acknowledge(Message message)
    {
    }

    /// <inheritdoc cref="RejectAsync"/>
    public bool Reject(Message message, MessageRejectionReason? reason = null)
    {
        return true;
    }

    /// <inheritdoc cref="PurgeAsync"/>
    public void Purge()
    {
    }

    /// <inheritdoc cref="ReceiveAsync"/>
    public Message[] Receive(TimeSpan? timeOut = null)
    {
        return BrighterAsyncContext.Run(async () => await ReceiveAsync(timeOut));
    }

    /// <inheritdoc cref="NackAsync"/>
    public void Nack(Message message)
    {
    }

    /// <inheritdoc cref="RequeueAsync"/>
    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        return BrighterAsyncContext.Run(async () => await RequeueAsync(message, delay));
    }

    /// <summary>
    /// Disposes the underlying NATS subscription.
    /// </summary>
    public void Dispose()
    {
        BrighterAsyncContext.Run(async () => await DisposeAsync());
    }

    /// <summary>
    /// Disposes the underlying NATS subscription.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await subscription.DisposeAsync();
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "No messages available on the NATS subscription before the timeout")]
        public static partial void NoMessagesAvailable(ILogger logger);

        [LoggerMessage(LogLevel.Debug, "Requeueing message {MessageId} by republishing to NATS subject {Subject}")]
        public static partial void RequeueingMessage(ILogger logger, string messageId, string subject);

        [LoggerMessage(LogLevel.Debug, "Scheduling requeue of message {MessageId} with delay {Delay}")]
        public static partial void SchedulingRequeue(ILogger logger, string messageId, TimeSpan delay);

        [LoggerMessage(LogLevel.Warning, "Cannot requeue message {MessageId}: the original NATS message is missing from the message bag")]
        public static partial void CannotRequeueMessage(ILogger logger, string messageId);
    }
}
