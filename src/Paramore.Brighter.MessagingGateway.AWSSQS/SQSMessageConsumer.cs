// ***********************************************************************
// Assembly         : paramore.brighter.messaginggateway.awssqs
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
using System.Linq;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using Paramore.Brighter.MessagingGateway.AWSSQS.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// Class SqsMessageConsumer.
    /// </summary>
    public class SqsMessageConsumer : IAmAMessageConsumer 
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<SqsMessageConsumer>);

        private readonly AWSMessagingGatewayConnection _connection;
        private readonly string _queueName;
        private readonly string _topic;
        private readonly bool _isFifo;
        private readonly int _visibilityTimeoutInSeconds;
        private readonly Message _noopMessage = new Message();

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageConsumer"/> class.
        /// </summary>
        /// <param name="connection">The connection details used to connect to the SQS queue.</param>
        /// <param name="queueName">The name of the SQS Queue</param>
       public SqsMessageConsumer(
            AWSMessagingGatewayConnection connection, 
            string queueName)
        {
            _connection = connection;
            _queueName = queueName;
        }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout 
        public Message Receive(int timeoutInMilliseconds)
        {
            AmazonSQSClient client = null;
            Amazon.SQS.Model.Message sqsMessage = null;
            try
            {
                client = new AmazonSQSClient(_connection.Credentials, _connection.Region);
                var urlResponse = client.GetQueueUrlAsync(_queueName).Result;

                _logger.Value.DebugFormat("SqsMessageConsumer: Preparing to retrieve next message from queue {0}",
                    urlResponse.QueueUrl);

                var request = new ReceiveMessageRequest(urlResponse.QueueUrl)
                {
                    MaxNumberOfMessages = 1,
                    WaitTimeSeconds = (int)TimeSpan.FromMilliseconds(timeoutInMilliseconds).TotalSeconds
                };

                var receiveResponse = client.ReceiveMessageAsync(request).Result;

                sqsMessage = receiveResponse.Messages.SingleOrDefault();

            }
            catch (Exception e)
            {
                 _logger.Value.ErrorException("SqsMessageConsumer: There was an error listening to queue {0} ", e, _queueName );
                throw;
            }
            finally
            {
                client?.Dispose();
            }
                
            if (sqsMessage == null)
                    return _noopMessage; 
            
             var message = CreateMessage(sqsMessage);
            
            _logger.Value.InfoFormat("SqsMessageConsumer: Received message from queue {0}, message: {1}{2}",
                _queueName, Environment.NewLine, JsonConvert.SerializeObject(message));
 

            return message;
        }

        private Message CreateMessage(Amazon.SQS.Model.Message rawSqsMessage)
        {
            var sqsmessage = JsonConvert.DeserializeObject<SqsMessage>(rawSqsMessage.Body);

            var contractResolver = new MessageDefaultContractResolver();
            var settings = new JsonSerializerSettings {ContractResolver = contractResolver};

            var message = JsonConvert.DeserializeObject<Message>(sqsmessage.Message ?? rawSqsMessage.Body, settings);

            message.Header.Bag.Add("ReceiptHandle", rawSqsMessage.ReceiptHandle);

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
                using (var client = new AmazonSQSClient(_connection.Credentials, _connection.Region))
                {
                    var urlResponse = client.GetQueueUrlAsync(_queueName).Result;
                    client.DeleteMessageAsync(new DeleteMessageRequest(urlResponse.QueueUrl, receiptHandle));

                    _logger.Value.InfoFormat("SqsMessageConsumer: Deleted the message {0} with receipt handle {1} on the queue {2}", message.Id, receiptHandle, urlResponse.QueueUrl);
                }
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException("SqsMessageConsumer: Error during deleting the message {0} with receipt handle {1} on the queue {2}", exception, message.Id, receiptHandle, _queueName);
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
                _logger.Value.InfoFormat("SqsMessageConsumer: Rejecting the message {0} with receipt handle {1} on the queue {2} with requeue paramter {3}", message.Id, receiptHandle, _queueName, requeue);
                
                using (var client = new AmazonSQSClient(_connection.Credentials, _connection.Region))
                {
                    var urlResponse = client.GetQueueUrlAsync(_queueName).Result;
                    if (requeue)
                    {
                        client.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest(urlResponse.QueueUrl, receiptHandle, 0)).Wait();
                    }
                    else
                    {
                        client.DeleteMessageAsync(urlResponse.QueueUrl, receiptHandle).Wait();
                    }
                }

                _logger.Value.InfoFormat("SqsMessageConsumer: Message {0} with receipt handle {1} on the queue {2} with requeue paramter {3} has been rejected", message.Id, receiptHandle, _queueName, requeue);
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException("SqsMessageConsumer: Error during rejecting the message {0} with receipt handle {1} on the queue {2}", exception, message.Id, receiptHandle, _queueName);
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
                using (var client = new AmazonSQSClient(_connection.Credentials, _connection.Region))
                {
                    _logger.Value.InfoFormat("SqsMessageConsumer: Purging the queue {0}", _queueName);

                    var urlResponse = client.GetQueueUrlAsync(_queueName).Result;
                    client.PurgeQueueAsync(urlResponse.QueueUrl).Wait();

                    _logger.Value.InfoFormat("SqsMessageConsumer: Purged the queue {0}", _queueName);
                }
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException("SqsMessageConsumer: Error purging queue {0}", exception, _queueName);
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

                using (var client = new AmazonSQSClient(_connection.Credentials, _connection.Region))
                {
                    _logger.Value.InfoFormat("SqsMessageConsumer: requeueing the message {0}", message.Id);

                    message.Header.Bag.Remove("ReceiptHandle");
                    var urlResponse = client.GetQueueUrlAsync(_queueName).Result;
                    var request = new SendMessageRequest(urlResponse.QueueUrl, JsonConvert.SerializeObject(message))
                                  {
                                      DelaySeconds = (int)TimeSpan.FromMilliseconds(delayMilliseconds).TotalSeconds
                                  };

                    client.SendMessageAsync(request).Wait();
                }

                _logger.Value.InfoFormat("SqsMessageConsumer: requeued the message {0}", message.Id);
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException("SqsMessageConsumer: Error purging queue {0}", exception, _queueName);
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
