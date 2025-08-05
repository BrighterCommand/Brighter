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
using Paramore.Brighter.Tasks;
using Polly;
using Polly.Retry;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// A Sync and Async Message Producer for Azure Service Bus.
/// </summary>
public abstract class AzureServiceBusMessageProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync, IAmABulkMessageProducerAsync
{
    private readonly IServiceBusSenderProvider _serviceBusSenderProvider;
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
    protected AzureServiceBusMessageProducer(
        IServiceBusSenderProvider serviceBusSenderProvider, 
        AzureServiceBusPublication publication, 
        int bulkSendBatchSize = 10
    )
    {
        _serviceBusSenderProvider = serviceBusSenderProvider;
        _publication = publication;
        _bulkSendBatchSize = bulkSendBatchSize;
    }
        
    /// <summary>
    /// Dispose of the producer
    /// </summary>
    public void Dispose() { }
        
    /// <summary>
    /// Dispose of the producer
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync()
    {
        return new ValueTask(Task.CompletedTask);
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


    /// <summary>
    /// Creates message batches
    /// </summary>
    /// <param name="messages">A collection of messages to create batches for</param>
    public IEnumerable<IAmAMessageBatch> CreateBatches(IEnumerable<Message> messages)
    {
        var topics = messages.Select(m => m.Header.Topic).Distinct().ToArray();
        
        if (topics.Count() != 1)
        {
            Logger.LogError("Cannot Bulk send for Multiple Topics, {NumberOfTopics} Topics Requested", topics.Count());
            throw new Exception($"Cannot Bulk send for Multiple Topics, {topics.Count()} Topics Requested");
        }

        var topic = topics.First()!;

        var batches = Enumerable.Range(0, (int)Math.Ceiling(messages.Count() / (decimal)_bulkSendBatchSize))
            .Select(i => new MessageBatch(new List<Message>(messages
                .Skip(i * _bulkSendBatchSize)
                .Take(_bulkSendBatchSize)
                .ToArray()))).ToArray();

        Logger.LogInformation("Sending Messages for {TopicName} split into {NumberOfBatches} Batches of {BatchSize}", topic, batches.Count(), _bulkSendBatchSize);

        return batches;
    }

    /// <summary>
    /// Sends a batch of messages.
    /// </summary>
    /// <param name="batch">A batch of messages to send</param>
    /// <param name="cancellationToken">The Cancellation Token.</param>
    /// <exception cref="NotImplementedException"></exception>
    public async Task SendAsync(IAmAMessageBatch batch, CancellationToken cancellationToken)
    {
        if (batch is not MessageBatch messageBatch)
            throw new NotImplementedException($"{nameof(SendAsync)} only supports ${typeof(MessageBatch)}");

        var topic = batch.RoutingKey;
        var serviceBusSenderWrapper = await GetSenderAsync(topic);

        Logger.LogInformation("Sending Batch of size {BatchSize} of Messages for {TopicName}", _bulkSendBatchSize, topic);
        
        try
        {
            var asbMessages = messageBatch!.Messages.Select(message 
                => AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message, _publication)).ToArray();

            Logger.LogDebug("Publishing {NumberOfMessages} messages to topic {Topic}.",
                asbMessages.Length, topic);

            await serviceBusSenderWrapper.SendAsync(asbMessages, cancellationToken);
        }
        finally
        {
            await serviceBusSenderWrapper.CloseAsync();
        }
    }

    /// <summary>
    /// Send the specified message with specified delay
    /// Sync over Async
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">Delay to delivery of the message.</param>
    public void SendWithDelay(Message message, TimeSpan? delay = null) => BrighterAsyncContext.Run(async () => await SendWithDelayAsync(message, delay));
       
    /// <summary>
    /// Send the specified message with specified delay
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">Delay delivery of the message.</param>
    /// <param name="cancellationToken">Cancel the in-flight send operation</param>
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Preparing  to send message on topic {Topic}", message.Header.Topic);
            
        delay ??= TimeSpan.Zero;

        if (message.Header.Topic is null) throw new ArgumentException("Topic not be null");

        var serviceBusSenderWrapper = await GetSenderAsync(message.Header.Topic);

        try
        {
            Logger.LogDebug(
                "Publishing message to topic {Topic} with a delay of {Delay} and body {Request} and id {Id}",
                message.Header.Topic, delay, message.Body.Value, message.Id);

            var azureServiceBusMessage = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message, _publication);
            if (delay == TimeSpan.Zero)
            {
                await serviceBusSenderWrapper.SendAsync(azureServiceBusMessage, cancellationToken);
            }
            else
            {
                var dateTimeOffset = new DateTimeOffset(DateTime.UtcNow.Add(delay.Value));
                await serviceBusSenderWrapper.ScheduleMessageAsync(azureServiceBusMessage, dateTimeOffset, cancellationToken);
            }

            Logger.LogDebug(
                "Published message to topic {Topic} with a delay of {Delay} and body {Request} and id {Id}", message.Header.Topic, delay, message.Body.Value, message.Id);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to publish message to topic {Topic} with id {Id}, message will not be retried", message.Header.Topic, message.Id);
            throw new ChannelFailureException("Error talking to the broker, see inner exception for details", e);
        }
        finally
        {
            await serviceBusSenderWrapper.CloseAsync();
        }
    }

    private async Task<IServiceBusSenderWrapper> GetSenderAsync(string topic)
    {
        await EnsureChannelExistsAsync(topic);

        try
        {
            RetryPolicy policy = Policy
                .Handle<Exception>()
                .Retry(TopicConnectionRetryCount, (exception, retryNumber) =>
                    {
                        Logger.LogError(exception, "Failed to connect to topic {Topic}, retrying...",
                            topic);

                        Thread.Sleep(TimeSpan.FromMilliseconds(TopicConnectionSleepBetweenRetriesInMilliseconds));
                    }
                );

            return policy.Execute(() => _serviceBusSenderProvider.Get(topic));
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to connect to topic {Topic}, aborting.", topic);
            throw;
        }
    }

    protected abstract Task EnsureChannelExistsAsync(string channelName);
}
