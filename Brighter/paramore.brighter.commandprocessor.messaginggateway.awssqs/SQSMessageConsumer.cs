// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.awssqs
// Author           : ian
// Created          : 08-17-2015
//
// Last Modified By : ian
// Last Modified On : 10-25-2015
// ***********************************************************************
// <copyright file="SqsMessageConsumer.cs" company="">
//     Copyright ©  2015
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    /// <summary>
    /// Class SqsMessageConsumer.
    /// </summary>
    public class SqsMessageConsumer : IAmAMessageConsumerSupportingDelay, IAmAMessageConsumerSupportingCache
    {
        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILog _logger;
        /// <summary>
        /// The _queue URL
        /// </summary>
        private readonly string _queueUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageConsumer"/> class.
        /// </summary>
        /// <param name="queueUrl">The queue URL.</param>
        public SqsMessageConsumer(string queueUrl)
            : this(queueUrl, LogProvider.GetCurrentClassLogger())
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageConsumer"/> class.
        /// Use this if you need to inject the logger, for example for testing
        /// </summary>
        /// <param name="queueUrl">The queue URL.</param>
        /// <param name="logger">The logger.</param>
        public SqsMessageConsumer(string queueUrl, ILog logger)
        {
            _logger = logger;
            _queueUrl = queueUrl;
            DelaySupported = true;
            CacheSupported = true;
        }

        /// <summary>
        /// Gets if the current provider configuration is able to support delayed delivery of messages.
        /// </summary>
        /// <value><c>true</c> if [delay supported]; otherwise, <c>false</c>.</value>
        public bool DelaySupported { get; private set; }

        /// <summary>
        /// Gets if the current provider configuration is able to support cached retrieval of messages.
        /// </summary>
        /// <value><c>true</c> if [cache supported]; otherwise, <c>false</c>.</value>
        public bool CacheSupported { get; private set; }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        public Message Receive(int timeoutInMilliseconds)
        {
            return Receive(timeoutInMilliseconds, 1);
        }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="noOfMessagesToCache">Number of cacheable messages.</param>
        /// <returns>Message.</returns>
        public Message Receive(int timeoutInMilliseconds, int noOfMessagesToCache)
        {
            _logger.DebugFormat("SqsMessageConsumer: Preparing to retrieve next message from queue {0}", _queueUrl);

            var rawSqsMessage = SqsQueuedRetriever.Instance.GetMessage(_queueUrl, timeoutInMilliseconds, noOfMessagesToCache).Result;

            if(rawSqsMessage == null)
                return new Message();

            var sqsmessage = JsonConvert.DeserializeObject<SqsMessage>(rawSqsMessage.Body);

            var contractResolver = new MessageDefaultContractResolver();
            var settings = new JsonSerializerSettings { ContractResolver = contractResolver };

            var message = JsonConvert.DeserializeObject<Message>(sqsmessage.Message ?? rawSqsMessage.Body, settings);

            message.Header.Bag.Add("ReceiptHandle", rawSqsMessage.ReceiptHandle);

            _logger.InfoFormat("SqsMessageConsumer: Received message from queue {0}, message: {1}{2}",
                    _queueUrl, Environment.NewLine, JsonConvert.SerializeObject(message));

            return message;
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            if(!message.Header.Bag.ContainsKey("ReceiptHandle"))
                return;

            var receiptHandle = message.Header.Bag["ReceiptHandle"].ToString();

            try
            {
                using (var client = new AmazonSQSClient())
                {
                    client.DeleteMessageAsync(new DeleteMessageRequest(_queueUrl, receiptHandle));

                    _logger.InfoFormat("SqsMessageConsumer: Deleted the message {0} with receipt handle {1} on the queue {2}", message.Id, receiptHandle, _queueUrl);
                }
            }
            catch (Exception exception)
            {
                _logger.ErrorException("SqsMessageConsumer: Error during deleting the message {0} with receipt handle {1} on the queue {2}", exception, message.Id, receiptHandle, _queueUrl);
                throw;
            }
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
        public void Reject(Message message, bool requeue)
        {
            if (!message.Header.Bag.ContainsKey("ReceiptHandle"))
                return;

            var receiptHandle = message.Header.Bag["ReceiptHandle"].ToString();

            try
            {
                _logger.InfoFormat("SqsMessageConsumer: Rejecting the message {0} with receipt handle {1} on the queue {2} with requeue paramter {3}", message.Id, receiptHandle, _queueUrl, requeue);
                
                using (var client = new AmazonSQSClient())
                {
                    if (requeue)
                    {
                        client.ChangeMessageVisibility(new ChangeMessageVisibilityRequest(_queueUrl, receiptHandle, 0));
                    }
                    else
                    {
                        client.DeleteMessage(_queueUrl, receiptHandle);
                    }
                }

                _logger.InfoFormat("SqsMessageConsumer: Message {0} with receipt handle {1} on the queue {2} with requeue paramter {3} has been rejected", message.Id, receiptHandle, _queueUrl, requeue);
            }
            catch (Exception exception)
            {
                _logger.ErrorException("SqsMessageConsumer: Error during rejecting the message {0} with receipt handle {1} on the queue {2}", exception, message.Id, receiptHandle, _queueUrl);
                throw;
            }
        }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public void Purge()
        {
            try
            {
                using (var client = new AmazonSQSClient())
                {
                    _logger.InfoFormat("SqsMessageConsumer: Purging the queue {0}", _queueUrl);

                    client.PurgeQueue(_queueUrl);

                    _logger.InfoFormat("SqsMessageConsumer: Purged the queue {0}", _queueUrl);
                }
            }
            catch (Exception exception)
            {
                _logger.ErrorException("SqsMessageConsumer: Error purging queue {0}", exception, _queueUrl);
                throw;
            }
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Requeue(Message message)
        {
            Requeue(message, 0);
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public void Requeue(Message message, int delayMilliseconds)
        {
            try
            {
                Reject(message, false);

                using (var client = new AmazonSQSClient())
                {
                    _logger.InfoFormat("SqsMessageConsumer: requeueing the message {0}", message.Id);

                    message.Header.Bag.Remove("ReceiptHandle");
                    var request = new SendMessageRequest(_queueUrl, JsonConvert.SerializeObject(message))
                                  {
                                      DelaySeconds = (int)TimeSpan.FromMilliseconds(delayMilliseconds).TotalSeconds
                                  };

                    client.SendMessage(request);
                }

                _logger.InfoFormat("SqsMessageConsumer: requeued the message {0}", message.Id);
            }
            catch (Exception exception)
            {
                _logger.ErrorException("SqsMessageConsumer: Error purging queue {0}", exception, _queueUrl);
                throw;
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
