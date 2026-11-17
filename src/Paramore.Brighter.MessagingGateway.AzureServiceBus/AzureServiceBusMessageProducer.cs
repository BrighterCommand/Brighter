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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;
using Polly;
using Polly.Retry;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// A Sync and Async Message Producer for Azure Service Bus.
/// </summary>
public abstract partial class AzureServiceBusMessageProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync, IAmABulkMessageProducerAsync
{
    private readonly IServiceBusSenderProvider _serviceBusSenderProvider;
    private readonly ConcurrentDictionary<string, IServiceBusSenderWrapper> _senders = new();
    //an int rather than a bool so Dispose can claim it with a single atomic Interlocked.Exchange: the
    //container and ProducerRegistry's finalizer may both try to close the cached senders, and they must
    //do it once
    private int _disposed;
    private readonly InstrumentationOptions _options;
    private AzureServiceBusPublication _publication;
    protected bool TopicCreated;
        
    private const int TopicConnectionSleepBetweenRetriesInMilliseconds = 100;
    private const int TopicConnectionRetryCount = 5;
    private readonly int _bulkSendBatchSize;
        
    protected abstract ILogger Logger { get; }

    /// <summary>
    /// The publication configuration for this producer
    /// </summary>
    public Publication Publication
    {
        get { return _publication; }
        set  {_publication = (AzureServiceBusPublication)value ?? throw new ConfigurationException("The publication must be an AzureServiceBusPublication"); }
}

    /// <summary>
    /// The OTel Span we are writing Producer events too
    /// </summary>
    public Activity? Span { get; set; }

    /// <inheritdoc />
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <summary>
    /// An Azure Service Bus Message producer <see cref="IAmAMessageProducer"/>
    /// </summary>
    /// <param name="serviceBusSenderProvider">The provider to use when producing messages.</param>
    /// <param name="publication">Configuration of a producer</param>
    /// <param name="bulkSendBatchSize">When sending more than one message using the MessageProducer, the max amount to send in a single transmission.</param>
    /// <param name="options"></param>
    protected AzureServiceBusMessageProducer(
        IServiceBusSenderProvider serviceBusSenderProvider, 
        AzureServiceBusPublication publication, 
        int bulkSendBatchSize = 10,
        InstrumentationOptions options = InstrumentationOptions.All
    )
    {
        _serviceBusSenderProvider = serviceBusSenderProvider;
        _publication = publication;
        _bulkSendBatchSize = bulkSendBatchSize;
        _options = options;
    }
        
    /// <summary>
    /// Dispose of the producer, closing the senders held for reuse
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        //reachable from ProducerRegistry's finalizer: with no sender cached there is nothing to close, so
        //skip the blocking wait for the async closes rather than block a finalizer thread on nothing
        if (_senders.IsEmpty) return;

        BrighterAsyncContext.Run(CloseCachedSendersAsync);
    }
        
    /// <summary>
    /// Dispose of the producer, closing the senders held for reuse
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        await CloseCachedSendersAsync();
    }

    /// <summary>
    /// Close and remove every cached sender, whether or not closing one of them fails: a close that
    /// throws must not strand the senders after it, and must not escape into the caller, which may be a
    /// finalizer
    /// </summary>
    private async Task CloseCachedSendersAsync()
    {
        //TryRemove first, so a sender cannot be closed twice and a concurrent send cannot add one that
        //nothing will ever close
        foreach (var topic in _senders.Keys)
        {
            if (!_senders.TryRemove(topic, out var sender)) continue;

            await CloseSenderQuietlyAsync(sender, topic);
        }
    }

    private async Task CloseSenderQuietlyAsync(IServiceBusSenderWrapper sender, string topic)
    {
        try
        {
            await sender.CloseAsync();
        }
        catch (Exception e)
        {
            Log.FailedToCloseCachedSender(Logger, e, topic);
        }
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
    /// Sends the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancel the in-flight send operation</param>
    public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        await SendWithDelayAsync(message, cancellationToken: cancellationToken);
    }


    /// <inheritdoc/>
    public async ValueTask<IEnumerable<IAmAMessageBatch>> CreateBatchesAsync(
        IEnumerable<Message> messages, CancellationToken cancellationToken)
    {
        var topics = messages.Select(m => m.Header.Topic).Distinct().ToArray();
        
        if (topics.Length != 1)
        {
            Log.CannotBulkSendForMultipleTopics(Logger, topics.Length);
            throw new Exception($"Cannot Bulk send for Multiple Topics, {topics.Length} Topics Requested");
        }

        var topic = topics.First()!;
        var serviceBusSenderWrapper = await GetSenderAsync(topic.Value);
        var azureServiceBusMessageBatches =
            new AzureServiceBusMessageBatches(serviceBusSenderWrapper, topic);

        foreach (Message message in messages)
            await azureServiceBusMessageBatches.AddMessageToBatchAsync(message, _publication.TimeToLive, cancellationToken);

        Log.SendingMessagesInBatches(Logger, topic, azureServiceBusMessageBatches.Batches.Count(), _bulkSendBatchSize);

        return azureServiceBusMessageBatches.Batches;
    }

    /// <inheritdoc/>
    /// <exception cref="NotImplementedException"></exception>
    public async Task SendAsync(IAmAMessageBatch batch, CancellationToken cancellationToken)
    {
        var topic = batch.RoutingKey;
        var messageCount = batch.Ids().Count();
        var serviceBusSenderWrapper = await GetSenderAsync(topic.Value);

        Log.SendingBatch(Logger, messageCount, topic);
        
        var sendTask = batch switch
        {
            AzureServiceBusSingleMessageBatch singleMessageBatch => serviceBusSenderWrapper.SendAsync(
                singleMessageBatch.Content, cancellationToken),
            AzureServiceBusMessageBatch serviceBusMessageBatch => serviceBusSenderWrapper.SendAsync(
                serviceBusMessageBatch.Content, cancellationToken),
            _ => throw new NotImplementedException(
                $"{nameof(SendAsync)} only supports ${typeof(AzureServiceBusSingleMessageBatch)} or ${typeof(AzureServiceBusMessageBatch)}")
        };
            
        await sendTask;
    }

    /// <summary>
    /// Send the specified message with specified delay
    /// Sync over Async
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">Delay to delivery of the message.</param>
    public void SendWithDelay(Message message, TimeSpan? delay = null) => BrighterAsyncContext.Run(() => SendWithDelayAsync(message, delay));
       
    /// <summary>
    /// Send the specified message with specified delay
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">Delay delivery of the message.</param>
    /// <param name="cancellationToken">Cancel the in-flight send operation</param>
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        Log.PreparingToSendMessage(Logger, message.Header.Topic);
            
        delay ??= TimeSpan.Zero;

        if (message.Header.Topic is null) throw new ArgumentException("Topic not be null");

        var destination = message.Header.Topic;
        var serviceBusSenderWrapper = await GetSenderAsync(destination.Value);

        try
        {
            Log.PublishingMessage(Logger, message.Header.Topic, delay, message.Body.Value, message.Id.Value);

            BrighterTracer.WriteProducerEvent(Span, "azure_service_bus", message, _options);
            var azureServiceBusMessage = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message, _publication.TimeToLive);
            if (delay == TimeSpan.Zero)
            {
                await serviceBusSenderWrapper.SendAsync(azureServiceBusMessage, cancellationToken);
            }
            else
            {
                var dateTimeOffset = new DateTimeOffset(DateTime.UtcNow.Add(delay.Value));
                await serviceBusSenderWrapper.ScheduleMessageAsync(azureServiceBusMessage, dateTimeOffset, cancellationToken);
            }

            Log.PublishedMessage(Logger, message.Header.Topic, delay, message.Body.Value, message.Id.Value);
        }
        catch (Exception e)
        {
            Log.FailedToPublishMessage(Logger, e, message.Header.Topic, message.Id.Value);
            throw new ChannelFailureException("Error talking to the broker, see inner exception for details", e);
        }
    }

    private async Task<IServiceBusSenderWrapper> GetSenderAsync(string topic)
    {
        if (_senders.TryGetValue(topic, out var cachedSender))
            return cachedSender;

        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(AzureServiceBusMessageProducer));

        await EnsureChannelExistsAsync(topic);

        IServiceBusSenderWrapper createdSender;
        try
        {
            RetryPolicy policy = Policy
                .Handle<Exception>()
                .Retry(TopicConnectionRetryCount, (exception, retryNumber) =>
                    {
                        Log.FailedToConnectRetrying(Logger, exception, topic);

                        Thread.Sleep(TimeSpan.FromMilliseconds(TopicConnectionSleepBetweenRetriesInMilliseconds));
                    }
                );

            createdSender = policy.Execute(() => _serviceBusSenderProvider.Get(topic));
        }
        catch (Exception e)
        {
            Log.FailedToConnectAborting(Logger, e, topic);
            throw;
        }

        var sender = _senders.GetOrAdd(topic, createdSender);
        if (!ReferenceEquals(sender, createdSender))
            //the sender we created lost the race: close ours, but a failure here must not fail a send that
            //already has a usable cached sender
            await CloseSenderQuietlyAsync(createdSender, topic);

        if (Volatile.Read(ref _disposed) != 0)
        {
            //disposed while we were creating a sender: its cache entry was added after the close pass ran,
            //so nothing else will ever close it
            if (_senders.TryRemove(topic, out var strandedSender))
                await CloseSenderQuietlyAsync(strandedSender, topic);

            throw new ObjectDisposedException(nameof(AzureServiceBusMessageProducer));
        }

        return sender;
    }

    protected abstract Task EnsureChannelExistsAsync(string channelName);

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Error, "Cannot Bulk send for Multiple Topics, {NumberOfTopics} Topics Requested")]
        public static partial void CannotBulkSendForMultipleTopics(ILogger logger, int numberOfTopics);

        [LoggerMessage(LogLevel.Information, "Sending Messages for {TopicName} split into {NumberOfBatches} Batches of {BatchSize}")]
        public static partial void SendingMessagesInBatches(ILogger logger, RoutingKey topicName, int numberOfBatches, int batchSize);

        [LoggerMessage(LogLevel.Information, "Sending Batch of size {BatchSize} of Messages for {TopicName}")]
        public static partial void SendingBatch(ILogger logger, int batchSize, RoutingKey topicName);

        [LoggerMessage(LogLevel.Debug, "Preparing to send message on topic {Topic}")]
        public static partial void PreparingToSendMessage(ILogger logger, RoutingKey topic);

        [LoggerMessage(LogLevel.Debug, "Publishing message to topic {Topic} with a delay of {Delay} and body {Request} and id {Id}")]
        public static partial void PublishingMessage(ILogger logger, RoutingKey topic, TimeSpan? delay, string request, string id);

        [LoggerMessage(LogLevel.Debug, "Published message to topic {Topic} with a delay of {Delay} and body {Request} and id {Id}")]
        public static partial void PublishedMessage(ILogger logger, RoutingKey topic, TimeSpan? delay, string request, string id);

        [LoggerMessage(LogLevel.Error, "Failed to publish message to topic {Topic} with id {Id}, message will not be retried")]
        public static partial void FailedToPublishMessage(ILogger logger, Exception e, RoutingKey topic, string id);

        [LoggerMessage(LogLevel.Error, "Failed to connect to topic {Topic}, retrying...")]
        public static partial void FailedToConnectRetrying(ILogger logger, Exception e, string topic);

        [LoggerMessage(LogLevel.Error, "Failed to connect to topic {Topic}, aborting.")]
        public static partial void FailedToConnectAborting(ILogger logger, Exception e, string topic);

        [LoggerMessage(LogLevel.Warning, "Failed to close the cached sender for topic {Topic}")]
        public static partial void FailedToCloseCachedSender(ILogger logger, Exception e, string topic);
    }
}
