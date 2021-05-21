using System;
using System.Collections.Generic;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Logging;
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
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusConsumer>();
        private readonly OnMissingChannel _makeChannel;
        private readonly ReceiveMode _receiveMode;

        private const string _lockTokenKey = "LockToken";
        
        public AzureServiceBusConsumer(string topicName, string subscriptionName, IAmAMessageProducer messageProducer, IManagementClientWrapper managementClientWrapper, 
            IMessageReceiverProvider messageReceiverProvider, int batchSize = 10, ReceiveMode receiveMode = ReceiveMode.ReceiveAndDelete, OnMissingChannel makeChannels = OnMissingChannel.Create)
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
            s_logger.LogInformation(
                "Getting message receiver provider for topic {Topic} and subscription {ChannelName} with receive Mode {ReceiveMode}...",
                _topicName, _subscriptionName, _receiveMode);
            try
            {
                _messageReceiver = _messageReceiverProvider.Get(_topicName, _subscriptionName, _receiveMode);
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failed to get message receiver provider for topic {Topic} and subscription {ChannelName}.", _topicName, _subscriptionName);
            }
        }

        public Message[] Receive(int timeoutInMilliseconds)
        {
            s_logger.LogDebug(
                "Preparing to retrieve next message(s) from topic {Topic} via subscription {ChannelName} with timeout {Timeout} and batch size {BatchSize}.",
                _topicName, _subscriptionName, timeoutInMilliseconds, _batchSize);

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
                    s_logger.LogDebug("Message Receiver is closing...");
                    var message = new Message(new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_QUIT), new MessageBody(string.Empty));
                    messagesToReturn.Add(message);
                    return messagesToReturn.ToArray();
                }

                s_logger.LogError(e, "Failing to receive messages.");

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
                s_logger.LogWarning(
                    "Null message body received from topic {Topic} via subscription {ChannelName}.",
                    _topicName, _subscriptionName);
            }

            var messageBody = System.Text.Encoding.Default.GetString(azureServiceBusMessage.MessageBodyValue ?? Array.Empty<byte>());
            s_logger.LogDebug("Received message from topic {Topic} via subscription {ChannelName} with body {Request}.",
                _topicName, _subscriptionName, messageBody);
            MessageType messageType = GetMessageType(azureServiceBusMessage);
            var handledCount = GetHandledCount(azureServiceBusMessage);
            var headers = new MessageHeader(Guid.NewGuid(), _topicName, messageType, DateTime.UtcNow, handledCount, 0);
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

        private static int GetHandledCount(IBrokeredMessageWrapper azureServiceBusMessage)
        {
            var count = 0;
            if (azureServiceBusMessage.UserProperties != null && azureServiceBusMessage.UserProperties.ContainsKey("HandledCount"))
            {
                int.TryParse(azureServiceBusMessage.UserProperties["HandledCount"].ToString(), out count);
            }
            return count;
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
                s_logger.LogWarning("Message entity already exists with topic {Topic} and subscription {ChannelName}.", _topicName,
                    _subscriptionName);
                _subscriptionCreated = true;
            }
            catch (Exception e)
            {
                s_logger.LogError(e, "Failing to check or create subscription.");
                
                //The connection to Azure Service bus may have failed so we re-establish the connection.
                _managementClientWrapper.Reset();
                
                throw new ChannelFailureException("Failing to check or create subscription", e);
            }
        }

        public void Requeue(Message message, int delayMilliseconds)
        {
            var topic = message.Header.Topic;

            s_logger.LogInformation("Requeuing message with topic {Topic} and id {Id}.", topic, message.Id);

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
                    s_logger.LogDebug("Acknowledging Message with Id {Id} Lock Token : {LockToken}", message.Id, lockToken);

                    _messageReceiver.Complete(lockToken).Wait();
                }
                catch(Exception ex)
                {
                    s_logger.LogError(ex, "Error completing message with id {Id}", message.Id);
                    throw;
                }
            }
        }

        public void Reject(Message message)
        {
            //Only Reject if ReceiveMode is Peek
            if (_receiveMode.Equals(ReceiveMode.PeekLock))
            {
                try
                {
                    EnsureSubscription();
                    var lockToken = message.Header.Bag[_lockTokenKey].ToString();

                    if (string.IsNullOrEmpty(lockToken))
                        throw new Exception($"LockToken for message with id {message.Id} is null or empty");
                    s_logger.LogDebug("Dead Lettering Message with Id {Id} Lock Token : {LockToken}", message.Id, lockToken);

                    _messageReceiver.DeadLetter(lockToken).Wait();
                }
                catch (Exception ex)
                {
                    s_logger.LogError(ex, "Error Dead Lettering message with id {Id}", message.Id);
                    throw;
                }
            }
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
            s_logger.LogWarning("Purge method NOT IMPLEMENTED.");
        }

        public void Dispose()
        {
            s_logger.LogInformation("Disposing the consumer...");
            _messageReceiver.Close();
            s_logger.LogInformation("Consumer disposed.");
        }
    }
}
