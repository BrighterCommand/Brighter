#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
#endregion*

using System;
using System.Collections.Generic;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// Read messages from an SQS queue
    /// </summary>
    public class SqsMessageConsumer : IAmAMessageConsumer
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<SqsMessageConsumer>();

        private readonly AWSClientFactory _clientFactory;
        private readonly string _queueName;
        private readonly int _batchSize;
        private readonly bool _hasDlq;
        private readonly bool _rawMessageDelivery;
        private readonly Message _noopMessage = new Message();

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageConsumer"/> class.
        /// </summary>
        /// <param name="awsConnection">The awsConnection details used to connect to the SQS queue.</param>
        /// <param name="queueName">The name of the SQS Queue</param>
        /// <param name="routingKey">the SNS Topic we subscribe to</param>
        /// <param name="batchSize">The maximum number of messages to consume per call to SQS</param>
        /// <param name="hasDLQ">Do we have a DLQ attached to this queue?</param>
        /// <param name="rawMessageDelivery">Do we have Raw Message Delivery enabled?</param>
        public SqsMessageConsumer(
            AWSMessagingGatewayConnection awsConnection,
            string queueName,
            RoutingKey routingKey,
            int batchSize = 1,
            bool hasDLQ = false,
            bool rawMessageDelivery = true)
        {
            _clientFactory = new AWSClientFactory(awsConnection);
            _queueName = queueName;
            _batchSize = batchSize;
            _hasDlq = hasDLQ;
            _rawMessageDelivery = rawMessageDelivery;
        }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds. Anything greater than 0 uses long-polling  </param>
        public Message[] Receive(int timeoutInMilliseconds)
        {
            AmazonSQSClient client = null;
            Amazon.SQS.Model.Message[] sqsMessages;
            try
            {
                client = _clientFactory.CreateSqsClient();
                var urlResponse = client.GetQueueUrlAsync(_queueName).GetAwaiter().GetResult();

                s_logger.LogDebug("SqsMessageConsumer: Preparing to retrieve next message from queue {URL}",
                    urlResponse.QueueUrl);

                var request = new ReceiveMessageRequest(urlResponse.QueueUrl)
                {
                    MaxNumberOfMessages = _batchSize,
                    WaitTimeSeconds = (int)TimeSpan.FromMilliseconds(timeoutInMilliseconds).TotalSeconds,
                    MessageAttributeNames = new List<string> {"All"},
                    AttributeNames = new List<string> {"All"}
                };

                var receiveResponse = client.ReceiveMessageAsync(request).GetAwaiter().GetResult();

                sqsMessages = receiveResponse.Messages.ToArray();
            }
            catch (InvalidOperationException ioe)
            {
                s_logger.LogDebug("SqsMessageConsumer: Could not determine number of messages to retrieve");
                throw new ChannelFailureException("Error connecting to SQS, see inner exception for details", ioe);
            }
            catch (OperationCanceledException oce)
            {
                s_logger.LogDebug("SqsMessageConsumer: Could not find messages to retrieve");
                throw new ChannelFailureException("Error connecting to SQS, see inner exception for details", oce);
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "SqsMessageConsumer: There was an error listening to queue {ChannelName} ", _queueName);
                throw;
            }
            finally
            {
                client?.Dispose();
            }

            if (sqsMessages.Length == 0)
            {
                return new[] {_noopMessage};
            }

            var messages = new Message[sqsMessages.Length];
            for (int i = 0; i < sqsMessages.Length; i++)
            {
                var message = SqsMessageCreatorFactory.Create(_rawMessageDelivery).CreateMessage(sqsMessages[i]);
                s_logger.LogInformation("SqsMessageConsumer: Received message from queue {ChannelName}, message: {1}{Request}",
                    _queueName, Environment.NewLine, JsonSerializer.Serialize(message, JsonSerialisationOptions.Options));
                messages[i] = message;
            }

            return messages;
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            if (!message.Header.Bag.ContainsKey("ReceiptHandle"))
                return;

            var receiptHandle = message.Header.Bag["ReceiptHandle"].ToString();

            try
            {
                using (var client = _clientFactory.CreateSqsClient())
                {
                    var urlResponse = client.GetQueueUrlAsync(_queueName).Result;
                    client.DeleteMessageAsync(new DeleteMessageRequest(urlResponse.QueueUrl, receiptHandle)).Wait();

                    s_logger.LogInformation("SqsMessageConsumer: Deleted the message {Id} with receipt handle {ReceiptHandle} on the queue {URL}", message.Id, receiptHandle,
                        urlResponse.QueueUrl);
                }
            }
            catch (Exception exception)
            {
                s_logger.LogError(exception, "SqsMessageConsumer: Error during deleting the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}", message.Id, receiptHandle, _queueName);
                throw;
            }
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public bool Reject(Message message)
        {
            if (!message.Header.Bag.ContainsKey("ReceiptHandle"))
                return false;

            var receiptHandle = message.Header.Bag["ReceiptHandle"].ToString();

            try
            {
                s_logger.LogInformation(
                    "SqsMessageConsumer: Rejecting the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}",
                    message.Id, receiptHandle, _queueName
                    );

                using (var client = _clientFactory.CreateSqsClient())
                {
                    var urlResponse = client.GetQueueUrlAsync(_queueName).Result;
                    if (_hasDlq)
                    {
                        client.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest(urlResponse.QueueUrl, receiptHandle, 0)).Wait();
                    }
                    else
                    {
                        client.DeleteMessageAsync(urlResponse.QueueUrl, receiptHandle).Wait();
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                s_logger.LogError(exception, "SqsMessageConsumer: Error during rejecting the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}", message.Id, receiptHandle, _queueName);
                throw;
            }

            return false;
        }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public void Purge()
        {
            try
            {
                using (var client = _clientFactory.CreateSqsClient())
                {
                    s_logger.LogInformation("SqsMessageConsumer: Purging the queue {ChannelName}", _queueName);

                    var urlResponse = client.GetQueueUrlAsync(_queueName).Result;
                    client.PurgeQueueAsync(urlResponse.QueueUrl).Wait();

                    s_logger.LogInformation("SqsMessageConsumer: Purged the queue {ChannelName}", _queueName);
                }
            }
            catch (Exception exception)
            {
                s_logger.LogError(exception, "SqsMessageConsumer: Error purging queue {ChannelName}", _queueName);
                throw;
            }
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        /// <returns>True if the message was requeued successfully</returns>
        public bool Requeue(Message message, int delayMilliseconds)
        {
            if (!message.Header.Bag.ContainsKey("ReceiptHandle"))
                return false;

            var receiptHandle = message.Header.Bag["ReceiptHandle"].ToString();

            try
            {
                s_logger.LogInformation("SqsMessageConsumer: re-queueing the message {Id}", message.Id);

                using (var client = _clientFactory.CreateSqsClient())
                {
                    var urlResponse = client.GetQueueUrlAsync(_queueName).Result;
                    client.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest(urlResponse.QueueUrl, receiptHandle, 0)).Wait();
                }

                s_logger.LogInformation("SqsMessageConsumer: re-queued the message {Id}", message.Id);

                return true;
            }
            catch (Exception exception)
            {
                s_logger.LogError(exception, "SqsMessageConsumer: Error during re-queueing the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}", message.Id, receiptHandle, _queueName);
                return false;
            }
        }

        private string FindTopicArnByName(RoutingKey topicName)
        {
            using (var snsClient = _clientFactory.CreateSnsClient())
            {
                var topic = snsClient.FindTopicAsync(topicName.Value).GetAwaiter().GetResult();
                if (topic == null)
                    throw new BrokerUnreachableException($"Unable to find a Topic ARN for {topicName.Value}");
                return topic.TopicArn;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
