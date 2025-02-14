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

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ;

/// <summary>
/// Class ClientRequestHandler .
/// The <see cref="RmqMessageProducer"/> is used by a client to talk to a server and abstracts the infrastructure for inter-process communication away from clients.
/// It handles subscription establishment, request sending and error handling
/// </summary>
public class RmqMessageProducer : RmqMessageGateway, IAmAMessageProducerSync, IAmAMessageProducerAsync, ISupportPublishConfirmation
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageProducer>();
    private static readonly SemaphoreSlim s_lock = new(1, 1);

    private readonly RmqPublication _publication;
    private readonly ConcurrentDictionary<ulong, string> _pendingConfirmations = new();
    private readonly int _waitForConfirmsTimeOutInMilliseconds;

    /// <summary>
    /// Action taken when a message is published, following receipt of a confirmation from the broker
    /// see https://www.rabbitmq.com/blog/2011/02/10/introducing-publisher-confirms#how-confirms-work for more
    /// </summary>
    public event Action<bool, string>? OnMessagePublished;

    /// <summary>
    /// The publication configuration for this producer
    /// </summary>
    public Publication Publication { get { return _publication; } }

    /// <summary>
    /// The OTel Span we are writing Producer events too
    /// </summary>
    public Activity? Span { get; set; }
    
    /// <summary>
    /// The <see cref="IAmAMessageScheduler"/>
    /// </summary>
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RmqMessageGateway" /> class.
    /// </summary>
    /// <param name="connection">The subscription information needed to talk to RMQ</param>
    ///     Make Channels = Create
    public RmqMessageProducer(RmqMessagingGatewayConnection connection)
        : this(connection, new RmqPublication { MakeChannels = OnMissingChannel.Create })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RmqMessageGateway" /> class.
    /// </summary>
    /// <param name="connection">The subscription information needed to talk to RMQ</param>
    /// <param name="publication">How should we configure this producer. If not provided use default behaviours:
    ///     Make Channels = Create
    /// </param>
    public RmqMessageProducer(RmqMessagingGatewayConnection connection, RmqPublication? publication)
        : base(connection)
    {
        _publication = publication ?? new RmqPublication { MakeChannels = OnMissingChannel.Create };
        _waitForConfirmsTimeOutInMilliseconds = _publication.WaitForConfirmsTimeOutInMilliseconds;
    }

    /// <summary>
    /// Sends the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Send(Message message) => SendWithDelay(message);

    /// <summary>
    /// Send the specified message with specified delay
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">Delay to delivery of the message.</param>
    /// <returns>Task.</returns>
    public void SendWithDelay(Message message, TimeSpan? delay = null) => BrighterAsyncContext.Run(async () => await SendWithDelayAsync(message, delay, false));

    /// <summary>
    /// Sends the specified message
    /// NOTE: RMQ's client has no async support, so this is not actually async and will block whilst it sends 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken">Pass a cancellation token to kill the send operation</param>
    /// <returns></returns>
    public async Task SendAsync(Message message, CancellationToken cancellationToken = default) => await SendWithDelayAsync(message, null, cancellationToken);

    /// <inheritdoc />
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        => await SendWithDelayAsync(message, delay, true, cancellationToken);
    
    private async Task SendWithDelayAsync(Message message, TimeSpan? delay, bool useSchedulerAsync, CancellationToken cancellationToken = default)
    {
        if (Connection.Exchange is null) throw new ConfigurationException("RmqMessageProducer: Exchange is not set");
        if (Connection.AmpqUri is null) throw new ConfigurationException("RmqMessageProducer: Broker URL is not set");
        
        delay ??= TimeSpan.Zero;

        try
        {
            s_logger.LogDebug("RmqMessageProducer: Preparing  to send message via exchange {ExchangeName}",
                Connection.Exchange.Name);
            
            await EnsureBrokerAsync(makeExchange: _publication.MakeChannels, cancellationToken: cancellationToken);
            
            if (Channel is null) throw new ChannelFailureException($"RmqMessageProducer: Channel is not set for {_publication.Topic}");

            var rmqMessagePublisher = new RmqMessagePublisher(Channel, Connection);

            message.Persist = Connection.PersistMessages;
            Channel.BasicAcksAsync += OnPublishSucceeded;
            Channel.BasicNacksAsync += OnPublishFailed;

            s_logger.LogDebug(
                "RmqMessageProducer: Publishing message to exchange {ExchangeName} on subscription {URL} with a delay of {Delay} and topic {Topic} and persisted {Persist} and id {Id} and body: {Request}",
                Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(), delay.Value.TotalMilliseconds,
                message.Header.Topic, message.Persist, message.Id, message.Body.Value);

            _pendingConfirmations.TryAdd(await Channel.GetNextPublishSequenceNumberAsync(cancellationToken), message.Id);

            if (delay == TimeSpan.Zero || DelaySupported)
            {
                await rmqMessagePublisher.PublishMessageAsync(message, delay.Value, cancellationToken);
            }
            else if(useSchedulerAsync)
            {
                var schedulerAsync = (IAmAMessageSchedulerAsync)Scheduler!;
                await schedulerAsync.ScheduleAsync(message, delay.Value, cancellationToken);
            }
            else
            {
                var schedulerSync = (IAmAMessageSchedulerSync)Scheduler!;
                schedulerSync.Schedule(message, delay.Value);
            }

            s_logger.LogInformation(
                "RmqMessageProducer: Published message to exchange {ExchangeName} on broker {URL} with a delay of {Delay} and topic {Topic} and persisted {Persist} and id {Id} and message: {Request} at {Time}",
                Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(), delay,
                message.Header.Topic, message.Persist, message.Id,
                JsonSerializer.Serialize(message, JsonSerialisationOptions.Options), DateTime.UtcNow);
        }
        catch (IOException io)
        {
            s_logger.LogError(io,
                "RmqMessageProducer: Error talking to the socket on {URL}, resetting subscription",
                Connection.AmpqUri.GetSanitizedUri()
            );
            await ResetConnectionToBrokerAsync(cancellationToken);
            throw new ChannelFailureException("Error talking to the broker, see inner exception for details", io);
        }
    }

    public sealed override void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private Task OnPublishFailed(object sender, BasicNackEventArgs e)
    {
        if (_pendingConfirmations.TryGetValue(e.DeliveryTag, out var messageId))
        {
            OnMessagePublished?.Invoke(false, messageId);
            _pendingConfirmations.TryRemove(e.DeliveryTag, out _);
            s_logger.LogDebug("Failed to publish message: {MessageId}", messageId);
        }

        return Task.CompletedTask;
    }

    private Task OnPublishSucceeded(object sender, BasicAckEventArgs e)
    {
        if (_pendingConfirmations.TryGetValue(e.DeliveryTag, out var messageId))
        {
            OnMessagePublished?.Invoke(true, messageId);
            _pendingConfirmations.TryRemove(e.DeliveryTag, out _);
            s_logger.LogInformation("Published message: {MessageId}", messageId);
        }

        return Task.CompletedTask;
    }
}
