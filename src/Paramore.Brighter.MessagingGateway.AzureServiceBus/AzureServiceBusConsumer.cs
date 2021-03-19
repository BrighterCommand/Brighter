using System;
using System.Collections.Generic;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    public class AzureServiceBusConsumer : IAmAMessageConsumer
    {
        private readonly string _topicName;
        private readonly IAmAMessageProducer _messageProducer;
        private readonly IManagementClientWrapper _managementClientWrapper;
        private readonly IMessageReceiverProvider _messageReceiverProvider;
        private readonly int _batchSize;
        private IMessageReceiverWrapper _messageReceiver;
        private readonly string _subscriptionName;
        private bool _subscriptionCreated;
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<AzureServiceBusConsumer>);
        private readonly OnMissingChannel _makeChannel;
        private readonly ReceiveMode _receiveMode;

        private const string _lockTokenKey = "LockToken";
        
        public AzureServiceBusConsumer(string topicName, string subscriptionName, IAmAMessageProducer messageProducer, IManagementClientWrapper managementClientWrapper, 
            IMessageReceiverProvider messageReceiverProvider, int batchSize = 10, OnMissingChannel makeChannels = OnMissingChannel.Create, ReceiveMode receiveMode = ReceiveMode.ReceiveAndDelete)
        {
            _subscriptionName = subscriptionName;
            _topicName = topicName;
            _messageProducer = messageProducer;
            _managementClientWrapper = managementClientWrapper;
            _messageReceiverProvider = messageReceiverProvider;
            _batchSize = batchSize;
            _makeChannel = makeChannels;
            _receiveMode = receiveMode;
            
            GetMessageReceiverProvider();
        }

        private void GetMessageReceiverProvider()
        {
            _logger.Value.Info($"Getting message receiver provider for topic {_topicName} and subscription {_subscriptionName} with recieve Mode {_receiveMode}...");
            try
            {
                _messageReceiver = _messageReceiverProvider.Get(_topicName, _subscriptionName, _receiveMode);
            }
            catch (Exception e)
            {
                _logger.Value.ErrorException($"Failed to get message receiver provider for topic {_topicName} and subscription {_subscriptionName}.", e);
            }
        }

        public Message[] Receive(int timeoutInMilliseconds)
        {
            _logger.Value.Debug($"Preparing to retrieve next message(s) from topic {_topicName} via subscription {_subscriptionName} with timeout {timeoutInMilliseconds} and batch size {_batchSize}.");

            IEnumerable<IBrokeredMessageWrapper> messages;
            EnsureSubscription();

            var messagesToReturn = new List<Message>();

            try
            {
                messages = _messageReceiver.Receive(_batchSize, TimeSpan.FromMilliseconds(timeoutInMilliseconds)).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                if (_messageReceiver.IsClosedOrClosing)
                {
                    _logger.Value.Debug("Message Receiver is closing...");
                    var message = new Message(new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_QUIT), new MessageBody(string.Empty));
                    messagesToReturn.Add(message);
                    return messagesToReturn.ToArray();
                }

                _logger.Value.ErrorException("Failing to receive messages.", e);

                //The connection to Azure Service bus may have failed so we re-establish the connection.
                GetMessageReceiverProvider();
                
                throw new ChannelFailureException("Failing to receive messages.", e);
            }
            
            foreach (IBrokeredMessageWrapper azureServiceBusMessage in messages)
            {
                Message message = MapToBrighterMessage(azureServiceBusMessage);
                messagesToReturn.Add(message);
            }

            return messagesToReturn.ToArray();
        }

        private Message MapToBrighterMessage(IBrokeredMessageWrapper azureServiceBusMessage)
        {
            if (azureServiceBusMessage.MessageBodyValue == null)
            {
                _logger.Value.Warn($"Null message body received from topic {_topicName} via subscription {_subscriptionName}.");
            }

            var messageBody = System.Text.Encoding.Default.GetString(azureServiceBusMessage.MessageBodyValue ?? Array.Empty<byte>());
            _logger.Value.Debug($"Received message from topic {_topicName} via subscription {_subscriptionName} with body {messageBody}.");
            MessageType messageType = GetMessageType(azureServiceBusMessage);
            var headers = new MessageHeader(Guid.NewGuid(), _topicName, messageType);
            if(_receiveMode.Equals(ReceiveMode.PeekLock)) headers.Bag.Add(_lockTokenKey, azureServiceBusMessage.LockToken);
            var message = new Message(headers, new MessageBody(messageBody));
            return message;
        }

        private static MessageType GetMessageType(IBrokeredMessageWrapper azureServiceBusMessage)
        {
            if (azureServiceBusMessage.UserProperties == null ||!azureServiceBusMessage.UserProperties.ContainsKey("MessageType")) 
                return MessageType.MT_EVENT;

            if (Enum.TryParse(azureServiceBusMessage.UserProperties["MessageType"].ToString(), true, out MessageType messageType))
                return messageType;

            return MessageType.MT_EVENT;
        }

        private void EnsureSubscription()
        {
            const int maxDeliveryCount = 2000;

            if (_subscriptionCreated || _makeChannel.Equals(OnMissingChannel.Assume))
                return;

            try
            {
                if (_managementClientWrapper.SubscriptionExists(_topicName, _subscriptionName))
                {
                    _subscriptionCreated = true;
                    return;
                }

                if (_makeChannel.Equals(OnMissingChannel.Validate))
                {
                    throw new ChannelFailureException($"Subscription {_subscriptionName} does not exist on topic {_topicName} and missing channel mode set to Validate.");
                }

                _managementClientWrapper.CreateSubscription(_topicName, _subscriptionName, maxDeliveryCount);
                _subscriptionCreated = true;
            }
            catch (MessagingEntityAlreadyExistsException)
            {
                _logger.Value.Warn($"Message entity already exists with topic {_topicName} and subscription {_subscriptionName}.");
                _subscriptionCreated = true;
            }
            catch (Exception e)
            {
                _logger.Value.ErrorException("Failing to check or create subscription.", e);
                
                //The connection to Azure Service bus may have failed so we re-establish the connection.
                _managementClientWrapper.Reset();
                
                throw new ChannelFailureException("Failing to check or create subscription", e);
            }
        }

        public void Requeue(Message message, int delayMilliseconds)
        {
            var topic = message.Header.Topic;

            _logger.Value.Info($"Requeuing message with topic {topic} and id {message.Id}.");

            if (delayMilliseconds > 0)
            {
                _messageProducer.SendWithDelay(message, delayMilliseconds);
            }
            else
            {
                _messageProducer.Send(message);
            }
        }

        public void Acknowledge(Message message)
        {
            //Only ACK if ReceiveMode is Peek
            if (_receiveMode.Equals(ReceiveMode.PeekLock))
            {
                try
                {
                    EnsureSubscription();
                    var lockToken = message.Header.Bag[_lockTokenKey].ToString();

                    if (string.IsNullOrEmpty(lockToken))
                        throw new Exception($"LockToken for message with id {message.Id} is null or empty");
                    _logger.Value.Debug($"Acknowledging Message with Id {message.Id} Lock Token : {lockToken}");

                    _messageReceiver.Complete(lockToken).Wait();
                }
                catch(Exception ex)
                {
                    _logger.Value.ErrorException($"Error completing message with id {message.Id}", ex);
                    throw;
                }
            }
        }

        public void Reject(Message message)
        {
            _logger.Value.Warn("Reject method NOT IMPLEMENTED.");
        }

        public void Reject(Message message, bool requeue)
        {
            if (requeue)
            {
                Requeue(message, 0);
            }
        }

        public void Purge()
        {
            _logger.Value.Warn("Purge method NOT IMPLEMENTED.");
        }

        public void Dispose()
        {
            _logger.Value.Info("Disposing the consumer...");
            _messageReceiver.Close();
            _logger.Value.Info("Consumer disposed.");
        }
    }
}
