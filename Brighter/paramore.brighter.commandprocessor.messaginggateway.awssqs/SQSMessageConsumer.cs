using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Amazon.SQS;
using Amazon.SQS.Model;

using Newtonsoft.Json;

using paramore.brighter.commandprocessor.Logging;

using TimeSpan = System.TimeSpan;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    public class SqsMessageConsumer : IAmAMessageConsumerSupportingDelay, IAmAMessageConsumerSupportingCache
    {
        private readonly ILog _logger;
        private readonly string _queueUrl;

        public SqsMessageConsumer(string queueUrl, ILog logger)
        {
            _logger = logger;
            _queueUrl = queueUrl;
            DelaySupported = true;
            CacheSupported = true;
        }

        public bool DelaySupported { get; private set; }

        public bool CacheSupported { get; private set; }

        public Message Receive(int timeoutInMilliseconds)
        {
            return Receive(timeoutInMilliseconds, 1);
        }

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

        public void Requeue(Message message)
        {
            Requeue(message, 0);
        }

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

        public void Dispose()
        {
            
        }
    }
}
