using System;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    /// <summary>
    /// Implementation of <see cref="IAmAMessageConsumer"/> using Azure Service Bus for Transport.
    /// </summary>
    public abstract class AzureServiceBusConsumer : IAmAMessageConsumer
    {
        protected abstract string SubscriptionName { get; }
        protected abstract ILogger Logger { get; }

        protected readonly AzureServiceBusSubscription Subscription;
        protected readonly string RoutingKey;
        private readonly IAmAMessageProducerSync _messageProducerSync;
        protected readonly IAdministrationClientWrapper AdministrationClientWrapper;
        private readonly int _batchSize;
        protected IServiceBusReceiverWrapper ServiceBusReceiver;
        protected readonly AzureServiceBusSubscriptionConfiguration SubscriptionConfiguration;
        
        protected AzureServiceBusConsumer(AzureServiceBusSubscription subscription, IAmAMessageProducerSync messageProducerSync,
            IAdministrationClientWrapper administrationClientWrapper)
        {
            Subscription = subscription;
            RoutingKey = subscription.RoutingKey;
            _batchSize = subscription.BufferSize;
            SubscriptionConfiguration = subscription.Configuration ?? new AzureServiceBusSubscriptionConfiguration();
            _messageProducerSync = messageProducerSync;
            AdministrationClientWrapper = administrationClientWrapper;
        }

        /// <summary>
        /// Receives the specified queue name.
        /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge the processing of those messages or requeue them.
        /// Used by a <see cref="Channel"/> to provide access to a third-party message queue.
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        public Message[] Receive(int timeoutInMilliseconds)
        {
            Logger.LogDebug(
                "Preparing to retrieve next message(s) from topic {Topic} via subscription {ChannelName} with timeout {Timeout} and batch size {BatchSize}",
                RoutingKey, SubscriptionName, timeoutInMilliseconds, _batchSize);

            IEnumerable<IBrokeredMessageWrapper> messages;
            EnsureChannel();

            var messagesToReturn = new List<Message>();

            try
            {
                if (SubscriptionConfiguration.RequireSession || ServiceBusReceiver == null)
                {
                    GetMessageReceiverProvider();
                    if (ServiceBusReceiver == null)
                    {
                        Logger.LogInformation("Message Gateway: Could not get a lock on a session for {TopicName}",
                            RoutingKey);
                        return messagesToReturn.ToArray();   
                    }
                }

                messages = ServiceBusReceiver.Receive(_batchSize, TimeSpan.FromMilliseconds(timeoutInMilliseconds))
                    .GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                if (ServiceBusReceiver is {IsClosedOrClosing: true} && !SubscriptionConfiguration.RequireSession)
                {
                    Logger.LogDebug("Message Receiver is closing...");
                    var message = new Message(
                        new MessageHeader(string.Empty, RoutingKey, MessageType.MT_QUIT), 
                        new MessageBody(string.Empty));
                    messagesToReturn.Add(message);
                    return messagesToReturn.ToArray();
                }

                Logger.LogError(e, "Failing to receive messages");

                //The connection to Azure Service bus may have failed so we re-establish the connection.
                if(!SubscriptionConfiguration.RequireSession || ServiceBusReceiver == null)
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

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        /// <returns>True if the message should be acked, false otherwise</returns>
        public bool Requeue(Message message, int delayMilliseconds)
        {
            var topic = message.Header.Topic;

            Logger.LogInformation("Requeuing message with topic {Topic} and id {Id}", topic, message.Id);

            if (delayMilliseconds > 0)
            {
                _messageProducerSync.SendWithDelay(message, delayMilliseconds);
            }
            else
            {
                _messageProducerSync.Send(message);
            }
            Acknowledge(message);

            return true;
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            try
            {
                EnsureChannel();
                var lockToken = message.Header.Bag[ASBConstants.LockTokenHeaderBagKey].ToString();

                if (string.IsNullOrEmpty(lockToken))
                    throw new Exception($"LockToken for message with id {message.Id} is null or empty");
                Logger.LogDebug("Acknowledging Message with Id {Id} Lock Token : {LockToken}", message.Id,
                    lockToken);
                
                if(ServiceBusReceiver == null)
                    GetMessageReceiverProvider();

                ServiceBusReceiver.Complete(lockToken).Wait();
                if (SubscriptionConfiguration.RequireSession)
                    ServiceBusReceiver.Close();
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is ServiceBusException asbException)
                    HandleAsbException(asbException, message.Id);
                else
                {
                    Logger.LogError(ex, "Error completing peak lock on message with id {Id}", message.Id);
                    throw;
                }
            }
            catch (ServiceBusException ex)
            {
                HandleAsbException(ex, message.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error completing peak lock on message with id {Id}", message.Id);
                throw;
            }
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Reject(Message message)
        {
            try
            {
                EnsureChannel();
                var lockToken = message.Header.Bag[ASBConstants.LockTokenHeaderBagKey].ToString();

                if (string.IsNullOrEmpty(lockToken))
                    throw new Exception($"LockToken for message with id {message.Id} is null or empty");
                Logger.LogDebug("Dead Lettering Message with Id {Id} Lock Token : {LockToken}", message.Id, lockToken);

                if(ServiceBusReceiver == null)
                    GetMessageReceiverProvider();
                
                ServiceBusReceiver.DeadLetter(lockToken).Wait();
                if (SubscriptionConfiguration.RequireSession)
                    ServiceBusReceiver.Close();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error Dead Lettering message with id {Id}", message.Id);
                throw;
            }
        }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public abstract void Purge();

        /// <summary>
        /// Dispose of the Consumer.
        /// </summary>
        public void Dispose()
        {
            Logger.LogInformation("Disposing the consumer...");
            ServiceBusReceiver?.Close();
            Logger.LogInformation("Consumer disposed");
        }

        protected abstract void GetMessageReceiverProvider();

        private Message MapToBrighterMessage(IBrokeredMessageWrapper azureServiceBusMessage)
        {
            if (azureServiceBusMessage.MessageBodyValue == null)
            {
                Logger.LogWarning(
                    "Null message body received from topic {Topic} via subscription {ChannelName}",
                    RoutingKey, SubscriptionName);
            }

            var messageBody = System.Text.Encoding.Default.GetString(azureServiceBusMessage.MessageBodyValue ?? Array.Empty<byte>());
            
            Logger.LogDebug("Received message from topic {Topic} via subscription {ChannelName} with body {Request}",
                RoutingKey, SubscriptionName, messageBody);
            
            MessageType messageType = GetMessageType(azureServiceBusMessage);
            var replyAddress = GetReplyAddress(azureServiceBusMessage);
            var handledCount = GetHandledCount(azureServiceBusMessage);
            
            //TODO:CLOUD_EVENTS parse from headers
            
            var headers = new MessageHeader(
                messageId: azureServiceBusMessage.Id, 
                topic:RoutingKey, 
                messageType: messageType, 
                source: null,
                type: "",
                timeStamp: DateTime.UtcNow,
                correlationId: azureServiceBusMessage.CorrelationId,
                replyTo: replyAddress,
                contentType: azureServiceBusMessage.ContentType,
                handledCount:handledCount, 
                dataSchema: null,
                subject: null,
                delayedMilliseconds: 0
                );

            headers.Bag.Add(ASBConstants.LockTokenHeaderBagKey, azureServiceBusMessage.LockToken);
            
            foreach (var property in azureServiceBusMessage.ApplicationProperties)
            {
                headers.Bag.Add(property.Key, property.Value);
            }
            
            var message = new Message(headers, new MessageBody(messageBody));
            return message;
        }

        private static MessageType GetMessageType(IBrokeredMessageWrapper azureServiceBusMessage)
        {
            if (azureServiceBusMessage.ApplicationProperties == null ||
                !azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.MessageTypeHeaderBagKey,
                    out object property))
                return MessageType.MT_EVENT;

            if (Enum.TryParse(property.ToString(), true, out MessageType messageType))
                return messageType;

            return MessageType.MT_EVENT;
        }

        private static string GetReplyAddress(IBrokeredMessageWrapper azureServiceBusMessage)
        {
            if (azureServiceBusMessage.ApplicationProperties is null ||
                !azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.ReplyToHeaderBagKey,
                    out object property))
            {
                return null;
            }

            var replyAddress = property.ToString();

            return replyAddress;
        }

        private static int GetHandledCount(IBrokeredMessageWrapper azureServiceBusMessage)
        {
            var count = 0;
            if (azureServiceBusMessage.ApplicationProperties != null &&
                azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.HandledCountHeaderBagKey,
                    out object property))
            {
                int.TryParse(property.ToString(), out count);
            }

            return count;
        }

        protected abstract void EnsureChannel();

        private void HandleAsbException(ServiceBusException ex, string messageId)
        {
            if (ex.Reason == ServiceBusFailureReason.MessageLockLost)
                Logger.LogError(ex, "Error completing peak lock on message with id {Id}", messageId);
            else
            {
                Logger.LogError(ex,
                    "Error completing peak lock on message with id {Id} Reason {ErrorReason}",
                    messageId, ex.Reason);
            }
        }
    }
}
