using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Polly;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Polly.Retry;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusMessageProducer : IAmAMessageProducer
    {
        public int MaxOutStandingMessages { get; set; } = -1;
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;

        private readonly IManagementClientWrapper _managementClientWrapper;
        private readonly ITopicClientProvider _topicClientProvider;
        private bool _topicCreated;

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusMessageProducer>();
        private const int TopicConnectionSleepBetweenRetriesInMilliseconds = 100;
        private const int TopicConnectionRetryCount = 5;
        private readonly OnMissingChannel _makeChannel;

        public AzureServiceBusMessageProducer(IManagementClientWrapper managementClientWrapper, ITopicClientProvider topicClientProvider, OnMissingChannel makeChannel = OnMissingChannel.Create)
        {
            _managementClientWrapper = managementClientWrapper;
            _topicClientProvider = topicClientProvider;
            _makeChannel = makeChannel;
        }

        public void Send(Message message)
        {
            SendWithDelay(message);
        }

        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            s_logger.LogDebug($"Preparing  to send message on topic {message.Header.Topic}");

            EnsureTopicExists(message.Header.Topic);

            ITopicClient topicClient;

            try
            {
                RetryPolicy policy = Policy
                    .Handle<Exception>()
                    .Retry(TopicConnectionRetryCount, (exception, retryNumber) =>
                    {
                        s_logger.LogError(exception, $"Failed to connect to topic {message.Header.Topic}, retrying...");

                        Thread.Sleep(TimeSpan.FromMilliseconds(TopicConnectionSleepBetweenRetriesInMilliseconds));
                    }
                    );

                topicClient = policy.Execute(() => _topicClientProvider.Get(message.Header.Topic));
            }
            catch (Exception e)
            {
                s_logger.LogError(e, $"Failed to connect to topic {message.Header.Topic}, aborting.");
                throw;
            }

            try
            {
                s_logger.LogDebug($"Publishing message to topic {message.Header.Topic} with a delay of {delayMilliseconds} and body {message.Body.Value} and id {message.Id}.");

                var azureServiceBusMessage = new Microsoft.Azure.ServiceBus.Message(message.Body.Bytes);
                azureServiceBusMessage.UserProperties.Add("MessageType", message.Header.MessageType.ToString());
                if (delayMilliseconds == 0)
                {
                    topicClient.Send(azureServiceBusMessage);
                }
                else
                {
                    var dateTimeOffset = new DateTimeOffset(DateTime.UtcNow.AddMilliseconds(delayMilliseconds));
                    topicClient.ScheduleMessage(azureServiceBusMessage, dateTimeOffset);
                }

                s_logger.LogDebug(
                    $"Published message to topic {message.Header.Topic} with a delay of {delayMilliseconds} and body {message.Body.Value} and id {message.Id}");
            }
            catch (Exception e)
            {
                s_logger.LogError(e, $"Failed to publish message to topic {message.Header.Topic} with id {message.Id}, message will not be retried.");
            }
            finally
            {
                topicClient.Close();
            }
        }

        private void EnsureTopicExists(string topic)
        {
            if (_topicCreated || _makeChannel.Equals(OnMissingChannel.Assume))
                return;

            try
            {
                if (_managementClientWrapper.TopicExists(topic))
                {
                    _topicCreated = true;
                    return;
                }

                if (_makeChannel.Equals(OnMissingChannel.Validate))
                {
                    throw new ChannelFailureException($"Topic {topic} does not exist and missing channel mode set to Validate.");
                }

                _managementClientWrapper.CreateTopic(topic);
                _topicCreated = true;
            }
            catch (Exception e)
            {
                //The connection to Azure Service bus may have failed so we re-establish the connection.
                _managementClientWrapper.Reset();
                s_logger.LogError(e, "Failing to check or create topic.");
                throw;
            }
        }

        public void Dispose()
        {
        }
    }
}
