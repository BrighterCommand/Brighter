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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.NATS.Extensions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Publishes Brighter messages to a NATS JetStream stream for a <see cref="NatsStreamPublication"/>.
/// </summary>
/// <remarks>
/// Publishing through JetStream persists the message in the stream, giving at-least-once delivery to stream
/// consumers. The server's pub-ack is verified on every send and doubles as the publish confirmation:
/// <see cref="OnMessagePublished"/> is raised with the outcome so the Outbox mediator only marks a message
/// dispatched once the server has stored it. Delayed sends are not supported by the transport itself;
/// <see cref="SendWithDelayAsync"/> delegates to the configured <see cref="Scheduler"/> and throws a
/// <see cref="ConfigurationException"/> if none is set.
/// </remarks>
/// <param name="jsContext">The <see cref="INatsJSContext"/> used to publish to the stream.</param>
/// <param name="publication">The <see cref="NatsStreamPublication"/> describing where messages are published.</param>
/// <param name="instrumentations">The <see cref="InstrumentationOptions"/> controlling how much telemetry is written.</param>
public partial class NatsStreamMessageProducer(
    INatsJSContext jsContext,
    NatsStreamPublication publication,
    InstrumentationOptions instrumentations) : IAmAMessageProducerAsync, IAmAMessageProducerSync, ISupportPublishConfirmation
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<NatsStreamMessageProducer>();

    /// <summary>
    /// Action taken when a message is published, following receipt of the JetStream pub-ack from the server.
    /// See https://www.rabbitmq.com/blog/2011/02/10/introducing-publisher-confirms#how-confirms-work for more.
    /// </summary>
    public event Action<PublishConfirmationResult>? OnMessagePublished;

    /// <inheritdoc/>
    public Publication Publication => publication;

    /// <inheritdoc/>
    public Activity? Span { get; set; }

    /// <inheritdoc/>
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <summary>
    /// Publishes the message to the publication's subject, persisting it in the JetStream stream.
    /// </summary>
    /// <remarks>Verifies the server's pub-ack so server-side failures (such as no stream capturing the subject) are not silently swallowed; the ack outcome is raised as the publish confirmation.</remarks>
    /// <param name="message">The <see cref="Message"/> to send; its headers are written as NATS headers and its body as the payload.</param>
    /// <param name="cancellationToken">Cancels the publish.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ChannelFailureException">Thrown when the JetStream server responds that the message was not stored.</exception>
    public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        BrighterTracer.WriteProducerEvent(Span, MessagingSystem.Nats, message, instrumentations);

        // Capture the publish span context synchronously, before any continuation runs, so the
        // confirmation can be linked back to the original publish.
        var publishContext = Activity.Current?.Context;
        Log.SendingMessageToStream(s_logger, publication.Topic!.Value, message.Id.Value);
        try
        {
            var ack = await jsContext.PublishAsync(publication.Topic!.Value, message.Body.ToByteArray(),
                headers: message.Header.ToNatsHeaders(),
                cancellationToken: cancellationToken);
            ack.EnsureSuccess();

            RaisePublishConfirmation(new PublishConfirmationResult(true, message.Id, publication.Topic, publishContext));
        }
        catch (NatsJSApiException ex)
        {
            Log.ErrorSendingMessageToStream(s_logger, ex, publication.Topic!.Value, message.Id.Value);
            RaisePublishConfirmation(new PublishConfirmationResult(false, message.Id, publication.Topic, publishContext));
            throw new ChannelFailureException("JetStream server rejected the publish, see inner exception for details", ex);
        }
    }

    /// <summary>
    /// Sends the message after the given delay, using the configured <see cref="Scheduler"/>.
    /// </summary>
    /// <remarks>JetStream has no native delayed delivery; a zero delay sends immediately.</remarks>
    /// <param name="message">The <see cref="Message"/> to send.</param>
    /// <param name="delay">How long to wait before sending; <see langword="null"/> or <see cref="TimeSpan.Zero"/> sends immediately.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ConfigurationException">Thrown when a non-zero delay is requested but no <see cref="Scheduler"/> is configured.</exception>
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        delay ??= TimeSpan.Zero;
        if (delay == TimeSpan.Zero)
        {
            await SendAsync(message, cancellationToken);
            return;
        }

        if (Scheduler is IAmAMessageSchedulerAsync asyncScheduler)
        {
            await asyncScheduler.ScheduleAsync(message, delay.Value, cancellationToken);
            return;
        }

        if (Scheduler is IAmAMessageSchedulerSync syncScheduler)
        {
            syncScheduler.Schedule(message, delay.Value);
            return;
        }

        throw new ConfigurationException(
            $"NatsStreamMessageProducer: delay of {delay} was requested but no scheduler is configured; configure a scheduler via MessageSchedulerFactory.");
    }

    /// <inheritdoc cref="SendAsync"/>
    public void Send(Message message)
    {
        BrighterAsyncContext.Run(async () => await SendAsync(message));
    }

    /// <inheritdoc cref="SendWithDelayAsync"/>
    public void SendWithDelay(Message message, TimeSpan? delay)
    {
        BrighterAsyncContext.Run(async () => await SendWithDelayAsync(message, delay));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }

    private void RaisePublishConfirmation(PublishConfirmationResult result)
    {
        // Raise on a worker thread so we never block the caller (which may be the single-threaded
        // BrighterAsyncContext on the sync path). Wrap the invoke so a faulting subscriber is logged
        // rather than left as an unobserved Task exception.
        _ = Task.Run(() =>
        {
            try
            {
                OnMessagePublished?.Invoke(result);
            }
            catch (Exception ex)
            {
                Log.PublishConfirmationRaiseFault(s_logger, ex);
            }
        });
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Sending message {MessageId} to JetStream subject {Subject}")]
        public static partial void SendingMessageToStream(ILogger logger, string subject, string messageId);

        [LoggerMessage(LogLevel.Error, "JetStream server rejected the publish of message {MessageId} to subject {Subject}")]
        public static partial void ErrorSendingMessageToStream(ILogger logger, Exception exception, string subject, string messageId);

        [LoggerMessage(LogLevel.Warning, "A publish-confirmation subscriber threw while handling a NATS JetStream pub-ack; the fault was contained")]
        public static partial void PublishConfirmationRaiseFault(ILogger logger, Exception exception);
    }
}
