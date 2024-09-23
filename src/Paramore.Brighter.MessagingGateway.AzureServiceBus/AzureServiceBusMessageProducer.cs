﻿#region Licence

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
using Microsoft.Extensions.Logging;
using Polly;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Polly.Retry;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// A Sync and Async Message Producer for Azure Service Bus.
    /// </summary>
    public abstract class AzureServiceBusMessageProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync, IAmABulkMessageProducerAsync
    {
        private readonly IServiceBusSenderProvider _serviceBusSenderProvider;
        private readonly AzureServiceBusPublication _publication;
        protected bool TopicCreated;
        
        private const int TopicConnectionSleepBetweenRetriesInMilliseconds = 100;
        private const int TopicConnectionRetryCount = 5;
        private readonly int _bulkSendBatchSize;
        
        protected abstract ILogger Logger { get; }

        /// <summary>
        /// The publication configuration for this producer
        /// </summary>
        public Publication Publication { get { return _publication; } }
        
        /// <summary>
        /// The OTel Span we are writing Producer events too
        /// </summary>
        public Activity? Span { get; set; }

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
        public async Task SendAsync(Message message)
        {
            await SendWithDelayAsync(message);
        }

        /// <summary>
        /// Sends a Batch of Messages
        /// </summary>
        /// <param name="messages">The messages to send.</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <returns>List of Messages successfully sent.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public async IAsyncEnumerable<string[]> SendAsync(
            IEnumerable<Message> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken
            )
        {
            var topics = messages.Select(m => m.Header.Topic).Distinct();
            if (topics.Count() != 1)
            {
                Logger.LogError("Cannot Bulk send for Multiple Topics, {NumberOfTopics} Topics Requested", topics.Count());
                throw new Exception($"Cannot Bulk send for Multiple Topics, {topics.Count()} Topics Requested");
            }
            var topic = topics.Single();

            var batches = Enumerable.Range(0, (int)Math.Ceiling(messages.Count() / (decimal)_bulkSendBatchSize))
                .Select(i => new List<Message>(messages
                    .Skip(i * _bulkSendBatchSize)
                    .Take(_bulkSendBatchSize)
                    .ToArray()));

            var serviceBusSenderWrapper = GetSender(topic);

            Logger.LogInformation("Sending Messages for {TopicName} split into {NumberOfBatches} Batches of {BatchSize}", topic, batches.Count(), _bulkSendBatchSize);
            try
            {
                foreach (var batch in batches)
                {
                    var asbMessages = batch.Select(ConvertToServiceBusMessage).ToArray();

                    Logger.LogDebug("Publishing {NumberOfMessages} messages to topic {Topic}.",
                        asbMessages.Length, topic);

                    await serviceBusSenderWrapper.SendAsync(asbMessages, cancellationToken);
                    yield return batch.Select(m => m.Id).ToArray();
                }
            }
            finally
            {
                await serviceBusSenderWrapper.CloseAsync();
            }
        }

        /// <summary>
        /// Send the specified message with specified delay
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delay">Delay to delivery of the message.</param>
        public void SendWithDelay(Message message, TimeSpan? delay = null)
        {
            delay ??= TimeSpan.Zero;
            SendWithDelayAsync(message, delay).Wait();
        }

        /// <summary>
        /// Send the specified message with specified delay
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delay">Delay delivery of the message.</param>
        public async Task SendWithDelayAsync(Message message, TimeSpan? delay = null)
        {
            Logger.LogDebug("Preparing  to send message on topic {Topic}", message.Header.Topic);
            
            delay ??= TimeSpan.Zero;

            var serviceBusSenderWrapper = GetSender(message.Header.Topic);

            try
            {
                Logger.LogDebug(
                    "Publishing message to topic {Topic} with a delay of {Delay} and body {Request} and id {Id}",
                    message.Header.Topic, delay, message.Body.Value, message.Id);

                var azureServiceBusMessage = ConvertToServiceBusMessage(message);
                if (delay == TimeSpan.Zero)
                {
                    await serviceBusSenderWrapper.SendAsync(azureServiceBusMessage);
                }
                else
                {
                    var dateTimeOffset = new DateTimeOffset(DateTime.UtcNow.Add(delay.Value));
                    await serviceBusSenderWrapper.ScheduleMessageAsync(azureServiceBusMessage, dateTimeOffset);
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

        public void Dispose()
        {
        }

        private IServiceBusSenderWrapper GetSender(string topic)
        {
            EnsureChannelExists(topic);

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

        private ServiceBusMessage ConvertToServiceBusMessage(Message message)
        {
            var azureServiceBusMessage = new ServiceBusMessage(message.Body.Bytes);
            azureServiceBusMessage.ApplicationProperties.Add(ASBConstants.MessageTypeHeaderBagKey, message.Header.MessageType.ToString());
            azureServiceBusMessage.ApplicationProperties.Add(ASBConstants.HandledCountHeaderBagKey, message.Header.HandledCount);
            azureServiceBusMessage.ApplicationProperties.Add(ASBConstants.ReplyToHeaderBagKey, message.Header.ReplyTo);

            foreach (var header in message.Header.Bag.Where(h => !ASBConstants.ReservedHeaders.Contains(h.Key)))
            {
                azureServiceBusMessage.ApplicationProperties.Add(header.Key, header.Value);
            }
            
            azureServiceBusMessage.CorrelationId = message.Header.CorrelationId;
            azureServiceBusMessage.ContentType = message.Header.ContentType;
            azureServiceBusMessage.MessageId = message.Header.Id;
            if (message.Header.Bag.TryGetValue(ASBConstants.SessionIdKey, out object? value))
                azureServiceBusMessage.SessionId = value.ToString();

            return azureServiceBusMessage;
        }

        protected abstract void EnsureChannelExists(string channelName);
    }
}
