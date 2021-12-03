using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
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
    public class AzureServiceBusMessageProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync
    {
        public int MaxOutStandingMessages { get; set; } = -1;
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;

        private readonly IAdministrationClientWrapper _administrationClientWrapper;
        private readonly IServiceBusSenderProvider _serviceBusSenderProvider;
        private bool _topicCreated;

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusMessageProducer>();
        private const int TopicConnectionSleepBetweenRetriesInMilliseconds = 100;
        private const int TopicConnectionRetryCount = 5;
        private readonly OnMissingChannel _makeChannel;

        public AzureServiceBusMessageProducer(IAdministrationClientWrapper administrationClientWrapper, IServiceBusSenderProvider serviceBusSenderProvider, OnMissingChannel makeChannel = OnMissingChannel.Create)
        {
            _administrationClientWrapper = administrationClientWrapper;
            _serviceBusSenderProvider = serviceBusSenderProvider;
            _makeChannel = makeChannel;
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
        /// Send the specified message with specified delay
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            SendWithDelayAsync(message, delayMilliseconds).Wait();
        }

        /// <summary>
        /// Send the specified message with specified delay
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public async Task SendWithDelayAsync(Message message, int delayMilliseconds = 0)
        {
            s_logger.LogDebug("Preparing  to send message on topic {Topic}", message.Header.Topic);

            EnsureTopicExists(message.Header.Topic);

            IServiceBusSenderWrapper serviceBusSenderWrapper;

            try
            {
                RetryPolicy policy = Policy
                    .Handle<Exception>()
                    .Retry(TopicConnectionRetryCount, (exception, retryNumber) =>
                    {
                        s_logger.LogError(exception, "Failed to connect to topic {Topic}, retrying...", message.Header.Topic);

                        Thread.Sleep(TimeSpan.FromMilliseconds(TopicConnectionSleepBetweenRetriesInMilliseconds));
                    }
                    );

                serviceBusSenderWrapper = policy.Execute(() => _serviceBusSenderProvider.Get(message.Header.Topic));
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failed to connect to topic {Topic}, aborting.", message.Header.Topic);
                throw;
            }

            try
            {
                s_logger.LogDebug(
                    "Publishing message to topic {Topic} with a delay of {Delay} and body {Request} and id {Id}.",
                    message.Header.Topic, delayMilliseconds, message.Body.Value, message.Id);

                var azureServiceBusMessage = new ServiceBusMessage(message.Body.Bytes);
                azureServiceBusMessage.ApplicationProperties.Add(ASBConstants.MessageTypeHeaderBagKey, message.Header.MessageType.ToString());
                azureServiceBusMessage.ApplicationProperties.Add(ASBConstants.HandledCountHeaderBagKey, message.Header.HandledCount);
                foreach (var header in message.Header.Bag.Where(h => !ASBConstants.ReservedHeaders.Contains(h.Key)))
                {
                    azureServiceBusMessage.ApplicationProperties.Add(header.Key, header.Value);
                }
                azureServiceBusMessage.CorrelationId = message.Header.CorrelationId.ToString();
                azureServiceBusMessage.ContentType = message.Header.ContentType;
                azureServiceBusMessage.MessageId = message.Header.Id.ToString();
                if (delayMilliseconds == 0)
                {
                    await serviceBusSenderWrapper.SendAsync(azureServiceBusMessage);
                }
                else
                {
                    var dateTimeOffset = new DateTimeOffset(DateTime.UtcNow.AddMilliseconds(delayMilliseconds));
                    await serviceBusSenderWrapper.ScheduleMessageAsync(azureServiceBusMessage, dateTimeOffset);
                }

                s_logger.LogDebug(
                    "Published message to topic {Topic} with a delay of {Delay} and body {Request} and id {Id}", message.Header.Topic, delayMilliseconds, message.Body.Value, message.Id);
                ;
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failed to publish message to topic {Topic} with id {Id}, message will not be retried.", message.Header.Topic, message.Id);
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

        private void EnsureTopicExists(string topic)
        {
            if (_topicCreated || _makeChannel.Equals(OnMissingChannel.Assume))
                return;

            try
            {
                if (_administrationClientWrapper.TopicExists(topic))
                {
                    _topicCreated = true;
                    return;
                }

                if (_makeChannel.Equals(OnMissingChannel.Validate))
                {
                    throw new ChannelFailureException($"Topic {topic} does not exist and missing channel mode set to Validate.");
                }

                _administrationClientWrapper.CreateTopic(topic);
                _topicCreated = true;
            }
            catch (Exception e)
            {
                //The connection to Azure Service bus may have failed so we re-establish the connection.
                _administrationClientWrapper.Reset();
                s_logger.LogError(e, "Failing to check or create topic.");
                throw;
            }
        }
    }
}
