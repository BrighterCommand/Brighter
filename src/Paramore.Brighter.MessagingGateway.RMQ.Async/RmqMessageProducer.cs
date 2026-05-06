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
using System.Collections.Generic;
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
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

/// <summary>
/// Class ClientRequestHandler .
/// The <see cref="RmqMessageProducer"/> is used by a client to talk to a server and abstracts the infrastructure for inter-process communication away from clients.
/// It handles subscription establishment, request sending and error handling
/// </summary>
public partial class RmqMessageProducer : RmqMessageGateway, IAmAMessageProducerSync, IAmAMessageProducerAsync, ISupportPublishConfirmation
{
    private readonly InstrumentationOptions _instrumentationOptions;
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageProducer>();

    // Used to bound the active-send wait when the user opts out of confirms (timeout=0).
    // Active sends in flight at dispose time should not be aborted: outbox would mark them Dispatched
    // while the broker may not yet have accepted the frame, producing duplicates on the next sweep.
    private const int DefaultActiveSendsShutdownTimeoutMs = 5000;

    private RmqPublication _publication;
    private readonly Dictionary<ulong, string> _pendingConfirmations = new();
    private readonly object _stateLock = new();
    private readonly int _waitForConfirmsTimeOutInMilliseconds;
    private TaskCompletionSource<bool> _activeSendsCompleted = NewCompletedTaskCompletionSource();
    private TaskCompletionSource<bool> _publisherConfirmationsCompleted = NewCompletedTaskCompletionSource();
    // Producer disposal has confirmation-specific work, so it has its own guard.
    // The base guard separately protects channel and pool cleanup after producer shutdown.
    private int _activeSends;
    private int _disposed;

    /// <summary>
    /// Action taken when a message is published, following receipt of a confirmation from the broker
    /// see https://www.rabbitmq.com/blog/2011/02/10/introducing-publisher-confirms#how-confirms-work for more
    /// </summary>
    public event Action<bool, string>? OnMessagePublished;

    /// <summary>
    /// The publication configuration for this producer
    /// </summary>
    public Publication Publication
    {
        get { return _publication; }
        set { _publication = (RmqPublication)value ?? throw new ArgumentNullException(nameof(value), "RmqMessageProducer: Publication cannot be null"); }
    }

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
    /// <param name="instrumentationOptions"> The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
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
    public void SendWithDelay(Message message, TimeSpan? delay = null) => BrighterAsyncContext.Run(() => SendWithDelayAsync(message, delay, false));

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
        // BeginSend is intentionally outside the try block; if it rejects a disposed producer, CompleteSend must not run.
        BeginSend();

        // Tracks the publish sequence we have registered for confirmation. Cleared once the publish succeeds
        // (broker takes ownership of the ack) or once we have explicitly removed the orphan in the catch path.
        ulong? pendingDeliveryTag = null;

        try
        {
            if (Connection.Exchange is null) throw new ConfigurationException("RmqMessageProducer: Exchange is not set");
            if (Connection.AmpqUri is null) throw new ConfigurationException("RmqMessageProducer: Broker URL is not set");

            delay ??= TimeSpan.Zero;

            Log.PreparingToSendAsync(s_logger, Connection.Exchange.Name);

            var channelInitialized = Channel is not null;
            await EnsureBrokerAsync(makeExchange: _publication.MakeChannels, cancellationToken: cancellationToken);

            if (Channel is null) throw new ChannelFailureException($"RmqMessageProducer: Channel is not set for {_publication.Topic}");
            if (!channelInitialized)
            {
                Channel.BasicAcksAsync += OnPublishSucceeded;
                Channel.BasicNacksAsync += OnPublishFailed;
            }

            message.Persist = Connection.PersistMessages;

            BrighterTracer.WriteProducerEvent(Span, MessagingSystem.RabbitMQ, message, _instrumentationOptions);

            Log.PublishingMessageAsync(s_logger, Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(), delay.Value.TotalMilliseconds,
                message.Header.Topic, message.Persist, message.Id, message.Body.Value);

            if (PublishesOnChannel(delay.Value))
            {
                var rmqMessagePublisher = new RmqMessagePublisher(Channel, Connection);
                var deliveryTag = await Channel.GetNextPublishSequenceNumberAsync(cancellationToken);
                AddPendingConfirmation(deliveryTag, message.Id);
                pendingDeliveryTag = deliveryTag;
                await rmqMessagePublisher.PublishMessageAsync(message, delay.Value, cancellationToken);
                // Publish succeeded; the broker now owns the confirmation and will ack/nack via the handler.
                pendingDeliveryTag = null;
            }
            else if (useSchedulerAsync)
            {
                var schedulerAsync = (IAmAMessageSchedulerAsync)Scheduler!;
                await schedulerAsync.ScheduleAsync(message, delay.Value, cancellationToken);
            }
            else
            {
                var schedulerSync = (IAmAMessageSchedulerSync)Scheduler!;
                schedulerSync.Schedule(message, delay.Value);
            }

            Log.PublishedMessageAsync(s_logger, Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(), delay,
                message.Header.Topic, message.Persist, message.Id,
                JsonSerializer.Serialize(message, JsonSerialisationOptions.Options), DateTime.UtcNow);
        }
        catch (IOException io)
        {
            Log.ErrorTalkingToSocketAsync(s_logger, io, Connection.AmpqUri!.GetSanitizedUri());
            ClearPendingConfirmations();
            // ClearPendingConfirmations removed the orphan; suppress the per-tag cleanup in finally.
            pendingDeliveryTag = null;
            // Capture the failed channel before reset; otherwise the detach fires on the recovered channel
            // and the next send loses confirm tracking.
            var failedChannel = Channel;
            await ResetConnectionToBrokerAsync(cancellationToken);
            if (failedChannel is not null)
            {
                failedChannel.BasicAcksAsync -= OnPublishSucceeded;
                failedChannel.BasicNacksAsync -= OnPublishFailed;
            }
            throw new ChannelFailureException("Error talking to the broker, see inner exception for details", io);
        }
        finally
        {
            // Non-IOException failures (broker timeouts, OperationInterruptedException, cancellation) leave
            // the registered tag in flight without a corresponding broker ack — remove it so disposal does
            // not block waiting on a confirmation that will never arrive.
            if (pendingDeliveryTag.HasValue)
                RemovePendingConfirmations(pendingDeliveryTag.Value, multiple: false);

            CompleteSend();
        }
    }

    public sealed override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        WaitForActiveSends();
        WaitForPendingPublisherConfirmations();
        DetachPublisherConfirmHandlers();

        var channel = Channel;
        if (channel is not null)
        {
            BrighterAsyncContext.Run(async () =>
            {
                await channel.AbortAsync();
                await channel.DisposeAsync();
            });
            // The base dispose still removes the pooled connection; the producer has already disposed the channel.
            Channel = null;
        }

        base.Dispose();
        // Explicit for symmetry with DisposeAsync; base.Dispose() also suppresses, so this is defensive.
        GC.SuppressFinalize(this);
    }

    public sealed override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        await WaitForActiveSendsAsync();
        await WaitForPendingPublisherConfirmationsAsync();
        DetachPublisherConfirmHandlers();

        var channel = Channel;
        if (channel is not null)
        {
            await channel.AbortAsync();
            await channel.DisposeAsync();
            // The base async dispose still removes the pooled connection; the producer has already disposed the channel.
            Channel = null;
        }

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private void BeginSend()
    {
        lock (_stateLock)
        {
            ThrowIfDisposed();

            if (_activeSends == 0)
                _activeSendsCompleted = NewPendingTaskCompletionSource();

            _activeSends++;
        }
    }

    private void CompleteSend()
    {
        lock (_stateLock)
        {
            _activeSends--;

            if (_activeSends == 0)
                _activeSendsCompleted.TrySetResult(true);
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(RmqMessageProducer));
    }

    private void WaitForActiveSends() => BrighterAsyncContext.Run(WaitForActiveSendsAsync);

    private async Task WaitForActiveSendsAsync()
    {
        Task activeSendsCompleted;

        lock (_stateLock)
        {
            if (_activeSends == 0)
                return;

            activeSendsCompleted = _activeSendsCompleted.Task;
        }

        // Always wait for in-flight sends, even when the user opted out of confirm waits (timeout=0).
        // See DefaultActiveSendsShutdownTimeoutMs comment for rationale.
        var waitMilliseconds = _waitForConfirmsTimeOutInMilliseconds > 0
            ? _waitForConfirmsTimeOutInMilliseconds
            : DefaultActiveSendsShutdownTimeoutMs;

        using var timeoutCancellation = new CancellationTokenSource();
        var timeout = Task.Delay(TimeSpan.FromMilliseconds(waitMilliseconds), timeoutCancellation.Token);
        var completed = await Task.WhenAny(activeSendsCompleted, timeout);

        if (completed == activeSendsCompleted)
        {
            timeoutCancellation.Cancel();
            return;
        }

        int activeSends;
        lock (_stateLock)
        {
            activeSends = _activeSends;
        }

        if (activeSends == 0)
            return;

        Log.FailedToAwaitActiveSends(s_logger, activeSends, waitMilliseconds);
    }

    private void WaitForPendingPublisherConfirmations() => BrighterAsyncContext.Run(WaitForPendingPublisherConfirmationsAsync);

    private async Task WaitForPendingPublisherConfirmationsAsync()
    {
        Task publisherConfirmationsCompleted;
        var waitMilliseconds = _waitForConfirmsTimeOutInMilliseconds;

        // _stateLock protects pending confirmations, not Channel. Disposal has already blocked new sends
        // and drained active sends, so the producer send path cannot replace Channel while this snapshot is taken.
        lock (_stateLock)
        {
            if (Channel is not { IsOpen: true } || _pendingConfirmations.Count == 0)
                return;

            if (waitMilliseconds == 0)
                return;

            publisherConfirmationsCompleted = _publisherConfirmationsCompleted.Task;
        }

        using var timeoutCancellation = new CancellationTokenSource();
        var timeout = Task.Delay(TimeSpan.FromMilliseconds(waitMilliseconds), timeoutCancellation.Token);
        var completed = await Task.WhenAny(publisherConfirmationsCompleted, timeout);

        if (completed == publisherConfirmationsCompleted)
        {
            timeoutCancellation.Cancel();
            return;
        }

        int pendingConfirmations;
        lock (_stateLock)
        {
            pendingConfirmations = _pendingConfirmations.Count;
        }

        if (pendingConfirmations == 0)
            return;

        Log.FailedToAwaitPublisherConfirms(s_logger, pendingConfirmations, waitMilliseconds);
    }

    private void AddPendingConfirmation(ulong deliveryTag, string messageId)
    {
        lock (_stateLock)
        {
            if (_pendingConfirmations.Count == 0)
                _publisherConfirmationsCompleted = NewPendingTaskCompletionSource();

            _pendingConfirmations[deliveryTag] = messageId;
        }
    }

    private void ClearPendingConfirmations()
    {
        lock (_stateLock)
        {
            _pendingConfirmations.Clear();
            _publisherConfirmationsCompleted.TrySetResult(true);
        }
    }

    private bool PublishesOnChannel(TimeSpan delay) => delay == TimeSpan.Zero || DelaySupported || Scheduler == null;

    private IReadOnlyCollection<string> RemovePendingConfirmations(ulong deliveryTag, bool multiple)
    {
        lock (_stateLock)
        {
            var deliveryTagsToRemove = new List<ulong>();

            foreach (var pendingDeliveryTag in _pendingConfirmations.Keys)
            {
                if (IsConfirmedBy(pendingDeliveryTag, deliveryTag, multiple))
                    deliveryTagsToRemove.Add(pendingDeliveryTag);
            }

            var messageIds = RemoveConfirmationsLocked(deliveryTagsToRemove);

            if (_pendingConfirmations.Count == 0)
                _publisherConfirmationsCompleted.TrySetResult(true);

            return messageIds;
        }
    }

    private List<string> RemoveConfirmationsLocked(IEnumerable<ulong> deliveryTagsToRemove)
    {
        var messageIds = new List<string>();

        foreach (var pendingDeliveryTag in deliveryTagsToRemove)
        {
            // Dictionary.Remove(key, out value) is unavailable on netstandard2.0; use the lookup-then-remove pattern.
            if (_pendingConfirmations.TryGetValue(pendingDeliveryTag, out var messageId))
            {
                _pendingConfirmations.Remove(pendingDeliveryTag);
                messageIds.Add(messageId);
            }
        }

        return messageIds;
    }

    private static bool IsConfirmedBy(ulong pendingDeliveryTag, ulong deliveryTag, bool multiple)
        => multiple ? pendingDeliveryTag <= deliveryTag : pendingDeliveryTag == deliveryTag;

    private static TaskCompletionSource<bool> NewCompletedTaskCompletionSource()
    {
        var taskCompletionSource = NewPendingTaskCompletionSource();
        taskCompletionSource.SetResult(true);
        return taskCompletionSource;
    }

    private static TaskCompletionSource<bool> NewPendingTaskCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void DetachPublisherConfirmHandlers()
    {
        Channel?.BasicAcksAsync -= OnPublishSucceeded;
        Channel?.BasicNacksAsync -= OnPublishFailed;
    }

    private Task OnPublishFailed(object sender, BasicNackEventArgs e)
    {
        foreach (var messageId in RemovePendingConfirmations(e.DeliveryTag, e.Multiple))
        {
            OnMessagePublished?.Invoke(false, messageId);
            Log.FailedToPublishMessageAsync(s_logger, messageId);
        }

        return Task.CompletedTask;
    }

    private Task OnPublishSucceeded(object sender, BasicAckEventArgs e)
    {
        foreach (var messageId in RemovePendingConfirmations(e.DeliveryTag, e.Multiple))
        {
            OnMessagePublished?.Invoke(true, messageId);
            Log.PublishedMessage(s_logger, messageId);
        }

        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "RmqMessageProducer: Preparing  to send message via exchange {ExchangeName}")]
        public static partial void PreparingToSendAsync(ILogger logger, string exchangeName);

        [LoggerMessage(LogLevel.Debug, "RmqMessageProducer: Publishing message to exchange {ExchangeName} on subscription {URL} with a delay of {Delay} and topic {Topic} and persisted {Persist} and id {Id} and body: {Request}")]
        public static partial void PublishingMessageAsync(ILogger logger, string exchangeName, string url, double delay, string topic, bool persist, string id, string request);

        [LoggerMessage(LogLevel.Information, "RmqMessageProducer: Published message to exchange {ExchangeName} on broker {URL} with a delay of {Delay} and topic {Topic} and persisted {Persist} and id {Id} and message: {Request} at {Time}")]
        public static partial void PublishedMessageAsync(ILogger logger, string exchangeName, string url, TimeSpan? delay, string topic, bool persist, string id, string request, DateTime time);

        [LoggerMessage(LogLevel.Error, "RmqMessageProducer: Error talking to the socket on {URL}, resetting subscription")]
        public static partial void ErrorTalkingToSocketAsync(ILogger logger, Exception exception, string url);
        
        [LoggerMessage(LogLevel.Debug, "Failed to publish message: {MessageId}")]
        public static partial void FailedToPublishMessageAsync(ILogger logger, string messageId);

        [LoggerMessage(LogLevel.Warning, "Failed to await {PendingCount} publisher confirms after {TimeoutMs}ms when shutting down")]
        public static partial void FailedToAwaitPublisherConfirms(ILogger logger, int pendingCount, int timeoutMs);

        [LoggerMessage(LogLevel.Warning, "Failed to await {ActiveSendCount} active sends after {TimeoutMs}ms when shutting down")]
        public static partial void FailedToAwaitActiveSends(ILogger logger, int activeSendCount, int timeoutMs);

        [LoggerMessage(LogLevel.Information, "Published message: {MessageId}")]
        public static partial void PublishedMessage(ILogger logger, string messageId);
    }
}

