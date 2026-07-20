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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
{
    /// <summary>
    /// Class ClientRequestHandler .
    /// The <see cref="RmqMessageProducer"/> is used by a client to talk to a server and abstracts the infrastructure for inter-process communication away from clients.
    /// It handles subscription establishment, request sending and error handling
    /// <remarks>This version of the consumer supports the RMQ V6 Client and its blocking API. For support of the V7 non-blocking API, please use
    /// the package Paramore.Brighter.MessagingGateway.RMQ.Async. As such, its SendAsync methods do not do true Async. Instead they rely on Run.Thread to mimic
    /// an Async operation.
    /// </remarks>
    /// </summary>
    public partial class RmqMessageProducer : RmqMessageGateway, IAmAMessageProducerSync, IAmAMessageProducerAsync, ISupportPublishConfirmation, ISupportPublishConfirmationAsync
    {
        private readonly InstrumentationOptions _instrumentationOptions;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageProducer>();

        static readonly object s_lock = new();
        private RmqPublication _publication;
        private readonly ConcurrentDictionary<ulong, PendingConfirmation> _pendingConfirmations = new ConcurrentDictionary<ulong, PendingConfirmation>();
        private bool _confirmsSelected;
        private readonly int _waitForConfirmsTimeOutInMilliseconds;
        private event Func<PublishConfirmationResult, Task>? _onMessagePublishedAsync;
        // The ack/nack handlers run on the client's connection loop, which must never block on a
        // subscriber, so awaited callbacks run on worker tasks. The tracker lets Dispose wait for
        // them — including the awaited Outbox mark-dispatched — after WaitForConfirms has drained
        // the broker acks themselves.
        private readonly InFlightCallbackTracker _confirmationCallbacks = new();

        /// <summary>
        /// Action taken when a message is published, following receipt of a confirmation from the broker
        /// see https://www.rabbitmq.com/blog/2011/02/10/introducing-publisher-confirms#how-confirms-work for more
        /// </summary>
        public event Action<PublishConfirmationResult>? OnMessagePublished;

        /// <inheritdoc cref="ISupportPublishConfirmationAsync.UseAsyncPublishConfirmation"/>
        bool ISupportPublishConfirmationAsync.UseAsyncPublishConfirmation => true;

        event Func<PublishConfirmationResult, Task> ISupportPublishConfirmationAsync.OnMessagePublishedAsync
        {
            add => _onMessagePublishedAsync += value;
            remove => _onMessagePublishedAsync -= value;
        }

        /// <summary>
        /// The publication configuration for this producer
        /// </summary>
        /// <value>A <see cref="RmqPublication"/></value>
        public Publication Publication
        {
            get { return _publication; }
            set { _publication = value as RmqPublication ?? throw new ConfigurationException("Publication must be of type RmqPublication"); }
        }

        /// <summary>
        /// The OTel Span we are writing Producer events too
        /// </summary>
        public Activity? Span { get; set; }

        /// <inheritdoc />
        public IAmAMessageScheduler? Scheduler { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageGateway" /> class.
        /// </summary>
        /// <param name="connection">The subscription information needed to talk to RMQ</param>
        /// <param name="instrumentationOptions">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
        /// Make Channels = Create
        public RmqMessageProducer(RmqMessagingGatewayConnection connection, InstrumentationOptions instrumentationOptions = InstrumentationOptions.All)
            : this(connection, new RmqPublication { MakeChannels = OnMissingChannel.Create })
        {
            _instrumentationOptions = instrumentationOptions;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageGateway" /> class.
        /// </summary>
        /// <param name="connection">The subscription information needed to talk to RMQ</param>
        /// <param name="publication">How should we configure this producer. If not provided use default behaviours:
        ///     Make Channels = Create
        /// </param>
        public RmqMessageProducer(RmqMessagingGatewayConnection connection, RmqPublication publication)
            : base(connection)
        {
            _publication = publication ?? new RmqPublication { MakeChannels = OnMissingChannel.Create };
            _waitForConfirmsTimeOutInMilliseconds = _publication.WaitForConfirmsTimeOutInMilliseconds;
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Send(Message message)
        {
            SendWithDelay(message);
        }

        /// <summary>
        /// Send the specified message with specified delay
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delay">Delay to delivery of the message.</param>
        /// <returns>Task.</returns>
        public void SendWithDelay(Message message, TimeSpan? delay = null) => SendWithDelay(message, delay, false);
        private void SendWithDelay(Message message, TimeSpan? delay, bool useSchedulerAsync)
        {
            delay ??= TimeSpan.Zero;

            // Capture the ambient publish context synchronously, before any send work, so the confirmation
            // raise (ack or nack) can link the settle span back to the producer (S1) span. Activity.Current
            // flows via AsyncLocal/ExecutionContext, so it is captured even when SendAsync routes via Task.Run.
            var publishContext = Activity.Current?.Context;

            try
            {
                lock (s_lock)
                {
                    EnsureBroker(makeExchange: _publication.MakeChannels);
                    //NOTE: EnsureBroker will create a channel if one does not exist
                    Log.PreparingToSend(s_logger, Connection.Exchange!.Name);

                    var rmqMessagePublisher = new RmqMessagePublisher(Channel!, Connection);

                    message.Persist = Connection.PersistMessages;
                    Channel!.BasicAcks += OnPublishSucceeded;
                    Channel.BasicNacks += OnPublishFailed;
                    Channel.ConfirmSelect();
                    _confirmsSelected = true;

                    BrighterTracer.WriteProducerEvent(Span, MessagingSystem.RabbitMQ, message, _instrumentationOptions);

                    Log.PublishingMessage(s_logger, Connection.Exchange.Name, Connection.AmpqUri!.GetSanitizedUri(), delay.Value.TotalMilliseconds,
                        message.Header.Topic.Value, message.Persist, message.Id.Value, message.Body.Value);

                    _pendingConfirmations.TryAdd(Channel.NextPublishSeqNo, new PendingConfirmation(message.Id, message.Header.Topic, publishContext));

                     if (delay == TimeSpan.Zero || DelaySupported || Scheduler == null)
                     {
                         rmqMessagePublisher.PublishMessage(message, delay.Value);
                     }
                     else if(useSchedulerAsync)
                     {
                         var schedulerAsync = (IAmAMessageSchedulerAsync)Scheduler!;
                         BrighterAsyncContext.Run(() => schedulerAsync.ScheduleAsync(message, delay.Value));
                     }
                     else
                     {
                         var schedulerSync = (IAmAMessageSchedulerSync)Scheduler!;
                         schedulerSync.Schedule(message, delay.Value);
                     }

                    Log.PublishedMessage(s_logger, Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(), delay,
                        message.Header.Topic.Value, message.Persist, message.Id.Value,
                        JsonSerializer.Serialize(message, JsonSerialisationOptions.Options), DateTime.UtcNow);
                }
            }
            catch (IOException io)
            {
                Log.ErrorTalkingToSocket(s_logger, io, Connection.AmpqUri!.GetSanitizedUri());
                ResetConnectionToBroker();
                throw new ChannelFailureException("Error talking to the broker, see inner exception for details", io);
            }
        }

        /// <summary>
        /// Sends the specified message
        /// NOTE: RMQ's client has no async support, so this is not actually async and will block whilst it sends 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="cancellationToken">Cancel the ongoing operation</param>
        /// <returns></returns>
        public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => Send(message), cancellationToken);
        }

        public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => SendWithDelay(message, delay), cancellationToken);
        }

        public sealed override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        public ValueTask DisposeAsync()
        {
            // The sync client has no async teardown, so dispose runs synchronously here; returning
            // default gives the caller a completed ValueTask. (Previously this returned a
            // TaskCompletionSource task that was never completed, so awaiting it hung forever.)
            Dispose(true);
            GC.SuppressFinalize(this);
            return default;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Channel != null && Channel.IsOpen && _confirmsSelected)
                {
                    //In the event this fails, then consequence is not marked as sent in outbox
                    //As we are disposing, just let that happen
                    Channel.WaitForConfirms(TimeSpan.FromMilliseconds(_waitForConfirmsTimeOutInMilliseconds), out bool timedOut);
                    if (timedOut)
                        Log.FailedToAwaitPublisherConfirms(s_logger);
                }

                // WaitForConfirms drains the broker acks; the callbacks those acks spawned (including
                // the awaited Outbox mark-dispatched) run on worker tasks, so wait for them too.
                WaitForConfirmationCallbacks();
            }

            base.Dispose(disposing);
        }

        private void OnPublishFailed(object? sender, BasicNackEventArgs e)
        {
            if (_pendingConfirmations.TryGetValue(e.DeliveryTag, out PendingConfirmation confirmation))
            {
                RaisePublishConfirmation(new PublishConfirmationResult(false, confirmation.MessageId, confirmation.Topic, confirmation.Context));
                _pendingConfirmations.TryRemove(e.DeliveryTag, out PendingConfirmation _);
                Log.FailedToPublishMessage(s_logger, confirmation.MessageId.Value);
            }
        }

        private void OnPublishSucceeded(object? sender, BasicAckEventArgs e)
        {
            if (_pendingConfirmations.TryGetValue(e.DeliveryTag, out PendingConfirmation confirmation))
            {
                RaisePublishConfirmation(new PublishConfirmationResult(true, confirmation.MessageId, confirmation.Topic, confirmation.Context));
                _pendingConfirmations.TryRemove(e.DeliveryTag, out PendingConfirmation _);
                Log.PublishedMessageInformation(s_logger, confirmation.MessageId.Value);
            }
        }

        private void RaisePublishConfirmation(PublishConfirmationResult result)
        {
            // The sync event stays on the connection loop thread, matching its long-standing behavior.
            OnMessagePublished?.Invoke(result);

            var handlers = _onMessagePublishedAsync;
            if (handlers is null)
                return;

            // Awaited callbacks must not block the connection loop (WaitForConfirms depends on it to
            // process acks), so they run on a worker task; Dispose waits on the in-flight tracker,
            // which must be released on every path or that wait would hang until its timeout.
            _confirmationCallbacks.Begin();
            Task.Run(async () =>
            {
                try
                {
                    await handlers.InvokeAllAsync(result);
                }
                catch (Exception ex)
                {
                    Log.ConfirmationCallbackFault(s_logger, result.MessageId.Value, ex);
                }
                finally
                {
                    _confirmationCallbacks.End();
                }
            });
        }

        private void WaitForConfirmationCallbacks()
        {
            // Timeout 0 means the user opted out of confirm waits at shutdown; honor that here too.
            if (_waitForConfirmsTimeOutInMilliseconds == 0)
                return;

            if (!_confirmationCallbacks.TryWait(TimeSpan.FromMilliseconds(_waitForConfirmsTimeOutInMilliseconds), out int stillInFlight))
                Log.FailedToAwaitConfirmationCallbacks(s_logger, stillInFlight, _waitForConfirmsTimeOutInMilliseconds);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "RmqMessageProducer: Preparing to send message via exchange {ExchangeName}")]
            public static partial void PreparingToSend(ILogger logger, string exchangeName);

            [LoggerMessage(LogLevel.Debug, "RmqMessageProducer: Publishing message to exchange {ExchangeName} on subscription {URL} with a delay of {Delay} and topic {Topic} and persisted {Persist} and id {Id} and body: {Request}")]
            public static partial void PublishingMessage(ILogger logger, string exchangeName, string url, double delay, string topic, bool persist, string id, string request);
            
            [LoggerMessage(LogLevel.Information, "RmqMessageProducer: Published message to exchange {ExchangeName} on broker {URL} with a delay of {Delay} and topic {Topic} and persisted {Persist} and id {Id} and message: {Request} at {Time}")]
            public static partial void PublishedMessage(ILogger logger, string exchangeName, string url, TimeSpan? delay, string topic, bool persist, string id, string request, DateTime time);

            [LoggerMessage(LogLevel.Error, "RmqMessageProducer: Error talking to the socket on {URL}, resetting subscription")]
            public static partial void ErrorTalkingToSocket(ILogger logger, Exception exception, string url);

            [LoggerMessage(LogLevel.Warning, "Failed to await publisher confirms when shutting down!")]
            public static partial void FailedToAwaitPublisherConfirms(ILogger logger);

            [LoggerMessage(LogLevel.Debug, "Failed to publish message: {MessageId}")]
            public static partial void FailedToPublishMessage(ILogger logger, string? messageId);

            [LoggerMessage(LogLevel.Information, "Published message: {MessageId}")]
            public static partial void PublishedMessageInformation(ILogger logger, string? messageId);

            [LoggerMessage(LogLevel.Warning, "Failed to await {CallbackCount} confirmation callbacks after {TimeoutMs}ms when shutting down")]
            public static partial void FailedToAwaitConfirmationCallbacks(ILogger logger, int callbackCount, int timeoutMs);

            [LoggerMessage(LogLevel.Warning, "Confirmation callback for message {MessageId} faulted; remaining confirmations continue")]
            public static partial void ConfirmationCallbackFault(ILogger logger, string messageId, Exception exception);
        }
    }

    /// <summary>
    /// Tracks the data needed to raise an enriched <see cref="PublishConfirmationResult"/> when the broker
    /// later acks or nacks a publish. Keyed by the publish sequence number (delivery tag) while the confirmation
    /// is in flight. The same entry feeds both the ack (<c>OnPublishSucceeded</c>) and nack (<c>OnPublishFailed</c>)
    /// handlers, so the enrichment is identical on success and failure.
    /// </summary>
    /// <param name="MessageId">The id of the published message.</param>
    /// <param name="Topic">The wire topic the message was published to (<c>message.Header.Topic</c>).</param>
    /// <param name="Context">The publish span context captured at send time, used to link the settle span; null when no <see cref="Activity"/> was active.</param>
    internal readonly record struct PendingConfirmation(Id MessageId, RoutingKey Topic, ActivityContext? Context);
}

