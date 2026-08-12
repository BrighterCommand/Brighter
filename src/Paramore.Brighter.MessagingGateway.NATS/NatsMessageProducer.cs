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
using NATS.Client.Core;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.NATS.Extensions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.NATS;

/// <summary>
/// Publishes Brighter messages to a core NATS subject for a <see cref="NatsPublication"/>.
/// </summary>
/// <remarks>
/// Core NATS provides at-most-once delivery: there is no broker-side persistence or acknowledgement.
/// Delayed sends are not supported by the transport itself; <see cref="SendWithDelayAsync"/> delegates to the
/// configured <see cref="Scheduler"/> and throws a <see cref="ConfigurationException"/> if none is set.
/// </remarks>
/// <param name="client">The <see cref="INatsClient"/> used to publish.</param>
/// <param name="publication">The <see cref="NatsPublication"/> describing where messages are published.</param>
/// <param name="instrumentation">The <see cref="InstrumentationOptions"/> controlling how much telemetry is written.</param>
public partial class NatsMessageProducer(
    INatsClient client,
    NatsPublication publication,
    InstrumentationOptions instrumentation) : IAmAMessageProducerAsync , IAmAMessageProducerSync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<NatsMessageProducer>();

    /// <inheritdoc/>
    public Publication Publication => publication;

    /// <inheritdoc/>
    public Activity? Span { get; set; }

    /// <inheritdoc/>
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <summary>
    /// Publishes the message to the publication's subject.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> to send; its headers are written as NATS headers and its body as the payload.</param>
    /// <param name="cancellationToken">Cancels the publish.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        BrighterTracer.WriteProducerEvent(Span, MessagingSystem.Nats, message, instrumentation);
        Log.SendingMessageToSubject(s_logger, publication.Topic!.Value, message.Id.Value);
        try
        {
            await client.PublishAsync(publication.Topic!.Value,
                message.Body.ToByteArray(),
                headers: message.Header.ToNatsHeaders(),
                replyTo: message.Header.ReplyTo?.Value,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Log.ErrorSendingMessageToSubject(s_logger, ex, publication.Topic!.Value, message.Id.Value);
            throw;
        }
    }

    /// <summary>
    /// Sends the message after the given delay, using the configured <see cref="Scheduler"/>.
    /// </summary>
    /// <remarks>Core NATS has no native delayed delivery; a zero delay sends immediately.</remarks>
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
            $"NatsMessageProducer: delay of {delay} was requested but no scheduler is configured; configure a scheduler via MessageSchedulerFactory.");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        
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

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Sending message {MessageId} to NATS subject {Subject}")]
        public static partial void SendingMessageToSubject(ILogger logger, string subject, string messageId);

        [LoggerMessage(LogLevel.Error, "Error sending message {MessageId} to NATS subject {Subject}")]
        public static partial void ErrorSendingMessageToSubject(ILogger logger, Exception exception, string subject, string messageId);
    }
}
