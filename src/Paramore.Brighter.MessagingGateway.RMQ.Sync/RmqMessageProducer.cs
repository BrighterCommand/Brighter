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
    public partial class RmqMessageProducer : RmqMessageGateway, IAmAMessageProducerSync, IAmAMessageProducerAsync, ISupportPublishConfirmation
    {
        private readonly InstrumentationOptions _instrumentationOptions;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageProducer>();

        static readonly object s_lock = new();
        private RmqPublication _publication;
        private readonly ConcurrentDictionary<ulong, string> _pendingConfirmations = new ConcurrentDictionary<ulong, string>();
        private bool _confirmsSelected;
        private readonly int _waitForConfirmsTimeOutInMilliseconds;

        /// <summary>
        /// Action taken when a message is published, following receipt of a confirmation from the broker
        /// see https://www.rabbitmq.com/blog/2011/02/10/introducing-publisher-confirms#how-confirms-work for more
        /// </summary>
        public event Action<bool, string>? OnMessagePublished;

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
                        message.Header.Topic, message.Persist, message.Id, message.Body.Value);

                    _pendingConfirmations.TryAdd(Channel.NextPublishSeqNo, message.Id);

                     if (delay == TimeSpan.Zero || DelaySupported || Scheduler == null)
                     {
                         rmqMessagePublisher.PublishMessage(message, delay.Value);
                     }
                     else if(useSchedulerAsync)
                     {
                         var schedulerAsync = (IAmAMessageSchedulerAsync)Scheduler!;
                         BrighterAsyncContext.Run(async () => await schedulerAsync.ScheduleAsync(message, delay.Value));
                     }
                     else
                     {
                         var schedulerSync = (IAmAMessageSchedulerSync)Scheduler!;
                         schedulerSync.Schedule(message, delay.Value);
                     }

                    Log.PublishedMessage(s_logger, Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(), delay,
                        message.Header.Topic, message.Persist, message.Id,
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
            await Task.Run(() => SendWithDelay(message), cancellationToken);
        }

        public sealed override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        public  ValueTask DisposeAsync()
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispose(true);
            GC.SuppressFinalize(this);
            return new ValueTask(tcs.Task);
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
            }

            base.Dispose(disposing);
        }

        private void OnPublishFailed(object? sender, BasicNackEventArgs e)
        {
            if (_pendingConfirmations.TryGetValue(e.DeliveryTag, out string? messageId))
            {
                OnMessagePublished?.Invoke(false, messageId);
                _pendingConfirmations.TryRemove(e.DeliveryTag, out string? _);
                Log.FailedToPublishMessage(s_logger, messageId);
            }
        }

        private void OnPublishSucceeded(object? sender, BasicAckEventArgs e)
        {
            if (_pendingConfirmations.TryGetValue(e.DeliveryTag, out string? messageId))
            {
                OnMessagePublished?.Invoke(true, messageId);
                _pendingConfirmations.TryRemove(e.DeliveryTag, out string? _);
                Log.PublishedMessageInformation(s_logger, messageId);
            }
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
        }
    }
}

