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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.NATS.Extensions;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Consumes messages from a NATS JetStream consumer on behalf of a <see cref="NatsStreamSubscription"/>.
/// </summary>
/// <remarks>
/// <para>
/// Messages are read one at a time from a single continuous consume loop
/// (<see cref="INatsJSConsumer.ConsumeAsync{T}(NATS.Client.Core.INatsDeserialize{T}, NatsJSConsumeOpts, CancellationToken)"/>)
/// created by <see cref="NatsMessageConsumerFactory"/>. The enumerator is kept alive for the lifetime of this
/// consumer: disposing an enumerator ends the underlying JetStream pull subscription, so a fresh one per receive
/// would stop delivery after the first message. A timed-out receive leaves the pending read in place for the
/// next call rather than cancelling it.
/// </para>
/// <para>
/// The original JetStream message travels in the Brighter message bag under <see cref="HeadersName.NatsMessage"/>
/// and is required for any acknowledgement operation; if it is absent the operation is a no-op.
/// Acknowledgement semantics follow JetStream: <see cref="AcknowledgeAsync"/> sends +ACK,
/// <see cref="RejectAsync"/> sends +TERM so a rejected message is never redelivered, and
/// <see cref="RequeueAsync"/> sends -NAK, optionally with a redelivery delay, so the message is redelivered.
/// </para>
/// <para>
/// Terminal consume-loop errors (for example the consumer being deleted, or a permanent connection failure)
/// surface as exceptions from <see cref="ReceiveAsync"/>; the consume loop does not recover by itself, so the
/// channel must be recreated to resume delivery.
/// </para>
/// </remarks>
/// <param name="stream">The <see cref="INatsJSStream"/> the subscription consumes from; used to purge the stream.</param>
/// <param name="messagesBuffer">The continuous stream of <see cref="INatsJSMsg{T}"/> pulled from the JetStream consumer.</param>
/// <param name="stopTokenSource">A <see cref="CancellationTokenSource"/> that stops <paramref name="messagesBuffer"/> when the consumer is disposed.</param>
public partial class NatsStreamMessageConsumer(
    INatsJSStream stream,
    IAsyncEnumerable<INatsJSMsg<byte[]>> messagesBuffer,
    CancellationTokenSource stopTokenSource) : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<NatsStreamMessageConsumer>();

    private IAsyncEnumerator<INatsJSMsg<byte[]>>? _enumerator;
    private Task<bool>? _pendingMoveNext;

    /// <summary>
    /// Acknowledges the message by sending +ACK to JetStream, removing it from the consumer's pending list.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to acknowledge; must carry the JetStream message in its bag.</param>
    /// <param name="cancellationToken">Cancels the acknowledgement.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!TryGetNatsMessage(message, out var natsMsg))
        {
            Log.CannotAcknowledgeMessage(s_logger, message.Id.Value);
            return;
        }

        Log.AcknowledgingMessage(s_logger, message.Id.Value);
        await natsMsg.AckAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Rejects the message by sending +TERM to JetStream, so it is never redelivered.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to reject; must carry the JetStream message in its bag.</param>
    /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message; sent to the server as the termination reason. Requires NATS Server 2.10.4+.</param>
    /// <param name="cancellationToken">Cancels the rejection.</param>
    /// <returns><see langword="true"/> if the message carried a JetStream message and was terminated; otherwise <see langword="false"/>.</returns>
    public async Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetNatsMessage(message, out var natsMsg))
        {
            Log.CannotRejectMessage(s_logger, message.Id.Value);
            return false;
        }

        Log.RejectingMessage(s_logger, message.Id.Value,
            reason?.Description ?? reason?.RejectionReason.ToString() ?? "unspecified");
        await natsMsg.AckTerminateAsync(
            new AckOpts { TerminateReason = reason?.Description ?? reason?.RejectionReason.ToString() },
            cancellationToken);
        return true;
    }

    /// <summary>
    /// Purges all messages from the JetStream stream backing this subscription.
    /// </summary>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        Log.PurgingStream(s_logger, stream.Info.Config.Name ?? string.Empty);
        await stream.PurgeAsync(new StreamPurgeRequest(), cancellationToken);
    }

    /// <summary>
    /// Receives the next message from the JetStream consumer, waiting up to <paramref name="timeOut"/>.
    /// </summary>
    /// <param name="timeOut">How long to wait for a message; if omitted, waits until <paramref name="cancellationToken"/> is cancelled.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>An array with the received <see cref="Message"/>, or an empty <see cref="Message"/> if none arrived before the timeout.</returns>
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        _enumerator ??= messagesBuffer.GetAsyncEnumerator(stopTokenSource.Token);

        var next = _enumerator.MoveNextAsync();
        if (next.IsCompleted)
        {
            return [_enumerator.Current.ToMessage()];
        }

        _pendingMoveNext ??= next.AsTask();

        var delay = Task.Delay(timeOut ?? Timeout.InfiniteTimeSpan, cancellationToken);
        var completed = await Task.WhenAny(_pendingMoveNext, delay).ConfigureAwait(false);

        if (completed != _pendingMoveNext)
        {
            // Timed out or cancelled; keep the pending read for the next call instead of abandoning it.
            Log.NoMessagesAvailable(s_logger);
            return [new Message()];
        }

        var moveNext = _pendingMoveNext;
        _pendingMoveNext = null;

        try
        {
            if (await moveNext.ConfigureAwait(false))
            {
                return [_enumerator.Current.ToMessage()];
            }
        }
        catch (OperationCanceledException)
        {
            // The consumer is being disposed mid-read.
        }

        return [new Message()];
    }

    /// <summary>
    /// Negative-acknowledges the message by sending -NAK to JetStream, triggering redelivery.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to nack; must carry the JetStream message in its bag.</param>
    /// <param name="cancellationToken">Cancels the nack.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!TryGetNatsMessage(message, out var natsMsg))
        {
            Log.CannotNackMessage(s_logger, message.Id.Value);
            return;
        }

        Log.NackingMessage(s_logger, message.Id.Value);
        await natsMsg.NakAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Requeues the message by sending -NAK to JetStream, so it is redelivered.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to requeue; must carry the JetStream message in its bag.</param>
    /// <param name="delay">How long JetStream should wait before redelivering; if omitted, redelivers immediately.</param>
    /// <param name="cancellationToken">Cancels the requeue.</param>
    /// <returns><see langword="true"/> if the message carried a JetStream message and was requeued; otherwise <see langword="false"/>.</returns>
    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetNatsMessage(message, out var natsMsg))
        {
            Log.CannotRequeueMessage(s_logger, message.Id.Value);
            return false;
        }

        Log.RequeueingMessage(s_logger, message.Id.Value, delay);
        var opts = delay.HasValue ? new AckOpts { NakDelay = delay.Value } : (AckOpts?)null;
        await natsMsg.NakAsync(opts, cancellationToken);
        return true;
    }

    /// <summary>
    /// Stops the JetStream consume loop feeding this consumer.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        stopTokenSource.Cancel();
        if (_enumerator is not null)
        {
            await _enumerator.DisposeAsync();
        }

        stopTokenSource.Dispose();
    }

    /// <inheritdoc cref="DisposeAsync"/>
    public void Dispose()
    {
        BrighterAsyncContext.Run(async () => await DisposeAsync());
    }

    /// <inheritdoc cref="AcknowledgeAsync"/>
    public void Acknowledge(Message message)
    {
        BrighterAsyncContext.Run(async () => await AcknowledgeAsync(message));
    }

    /// <inheritdoc cref="RejectAsync"/>
    public bool Reject(Message message, MessageRejectionReason? reason = null)
    {
        return BrighterAsyncContext.Run(async () => await RejectAsync(message, reason));
    }

    /// <inheritdoc cref="PurgeAsync"/>
    public void Purge()
    {
        BrighterAsyncContext.Run(async () => await PurgeAsync());
    }

    /// <inheritdoc cref="ReceiveAsync"/>
    public Message[] Receive(TimeSpan? timeOut = null)
    {
        return BrighterAsyncContext.Run(async () => await ReceiveAsync(timeOut));
    }

    /// <inheritdoc cref="NackAsync"/>
    public void Nack(Message message)
    {
        BrighterAsyncContext.Run(async () => await NackAsync(message));
    }

    /// <inheritdoc cref="RequeueAsync"/>
    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        return BrighterAsyncContext.Run(async () => await RequeueAsync(message, delay));
    }

    private static bool TryGetNatsMessage(Message message, out INatsJSMsg<byte[]> natsMsg)
    {
        if (message.Header.Bag.TryGetValue(HeadersName.NatsMessage, out var obj)
            && obj is INatsJSMsg<byte[]> jsMsg)
        {
            natsMsg = jsMsg;
            return true;
        }

        natsMsg = null!;
        return false;
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Acknowledging message {MessageId} (+ACK)")]
        public static partial void AcknowledgingMessage(ILogger logger, string messageId);

        [LoggerMessage(LogLevel.Warning,
            "Cannot acknowledge message {MessageId}: the JetStream message is missing from the message bag")]
        public static partial void CannotAcknowledgeMessage(ILogger logger, string messageId);

        [LoggerMessage(LogLevel.Debug, "Rejecting message {MessageId} (+TERM) with reason {Reason}")]
        public static partial void RejectingMessage(ILogger logger, string messageId, string reason);

        [LoggerMessage(LogLevel.Warning,
            "Cannot reject message {MessageId}: the JetStream message is missing from the message bag")]
        public static partial void CannotRejectMessage(ILogger logger, string messageId);

        [LoggerMessage(LogLevel.Debug, "Nacking message {MessageId} (-NAK)")]
        public static partial void NackingMessage(ILogger logger, string messageId);

        [LoggerMessage(LogLevel.Warning,
            "Cannot nack message {MessageId}: the JetStream message is missing from the message bag")]
        public static partial void CannotNackMessage(ILogger logger, string messageId);

        [LoggerMessage(LogLevel.Debug, "Requeueing message {MessageId} (-NAK) with redelivery delay {Delay}")]
        public static partial void RequeueingMessage(ILogger logger, string messageId, TimeSpan? delay);

        [LoggerMessage(LogLevel.Warning,
            "Cannot requeue message {MessageId}: the JetStream message is missing from the message bag")]
        public static partial void CannotRequeueMessage(ILogger logger, string messageId);

        [LoggerMessage(LogLevel.Debug, "No messages available on the JetStream consumer before the timeout")]
        public static partial void NoMessagesAvailable(ILogger logger);

        [LoggerMessage(LogLevel.Information, "Purging all messages from JetStream stream {StreamName}")]
        public static partial void PurgingStream(ILogger logger, string streamName);
    }
}
