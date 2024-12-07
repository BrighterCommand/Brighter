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
        protected readonly string Topic;
        private readonly IAmAMessageProducerSync _messageProducerSync;
        protected readonly IAdministrationClientWrapper AdministrationClientWrapper;
        private readonly int _batchSize;
        protected IServiceBusReceiverWrapper? ServiceBusReceiver;
        protected readonly AzureServiceBusSubscriptionConfiguration SubscriptionConfiguration;
        
        protected AzureServiceBusConsumer(AzureServiceBusSubscription subscription, IAmAMessageProducerSync messageProducerSync,
            IAdministrationClientWrapper administrationClientWrapper)
        {
            Subscription = subscription;
            Topic = subscription.RoutingKey;
            _batchSize = subscription.BufferSize;
            SubscriptionConfiguration = subscription.Configuration ?? new AzureServiceBusSubscriptionConfiguration();
            _messageProducerSync = messageProducerSync;
            AdministrationClientWrapper = administrationClientWrapper;
        }

        /// <summary>
        /// Receives the specified queue name.
        /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge
        /// the processing of those messages or requeue them.
        /// Used by a <see cref="Channel"/> to provide access to a third-party message queue.
        /// Sync over async,
        /// </summary>
        /// <param name="timeOut">The timeout for a message being available. Defaults to 300ms.</param>
        /// <returns>Message.</returns>
        public Message[] Receive(TimeSpan? timeOut = null)
        {
            Logger.LogDebug(
                "Preparing to retrieve next message(s) from topic {Topic} via subscription {ChannelName} with timeout {Timeout} and batch size {BatchSize}",
                Topic, SubscriptionName, timeOut, _batchSize);

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
                            Topic);
                        return messagesToReturn.ToArray();   
                    }
                }

                timeOut ??= TimeSpan.FromMilliseconds(300);
                
                messages = ServiceBusReceiver.Receive(_batchSize, timeOut.Value)
                    .GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                if (ServiceBusReceiver is {IsClosedOrClosing: true} && !SubscriptionConfiguration.RequireSession)
                {
                    Logger.LogDebug("Message Receiver is closing...");
                    var message = new Message(
                        new MessageHeader(string.Empty, new RoutingKey(Topic), MessageType.MT_QUIT), 
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
        /// <param name="delay">Delay to the delivery of the message. 0 is no delay. Defaults to 0.</param>
        /// <returns>True if the message should be acked, false otherwise</returns>
        public bool Requeue(Message message, TimeSpan? delay = null)
        {
            var topic = message.Header.Topic;
            delay ??= TimeSpan.Zero;

            Logger.LogInformation("Requeuing message with topic {Topic} and id {Id}", topic, message.Id);

            if (delay.Value > TimeSpan.Zero)
            {
                _messageProducerSync.SendWithDelay(message, delay.Value);
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

                ServiceBusReceiver?.Complete(lockToken)
                    .GetAwaiter()
                    .GetResult();
                
                if (SubscriptionConfiguration.RequireSession)
                    ServiceBusReceiver?.Close();
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
        /// Sync over Async
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
                
                ServiceBusReceiver?.DeadLetter(lockToken)
                    .GetAwaiter()
                    .GetResult();
                if (SubscriptionConfiguration.RequireSession)
                    ServiceBusReceiver?.Close();
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
                    Topic, SubscriptionName);
            }

            var messageBody = System.Text.Encoding.Default.GetString(azureServiceBusMessage.MessageBodyValue ?? Array.Empty<byte>());
            
            Logger.LogDebug("Received message from topic {Topic} via subscription {ChannelName} with body {Request}",
                Topic, SubscriptionName, messageBody);
            
            MessageType messageType = GetMessageType(azureServiceBusMessage);
            var replyAddress = GetReplyAddress(azureServiceBusMessage);
            var handledCount = GetHandledCount(azureServiceBusMessage);
            
            //TODO:CLOUD_EVENTS parse from headers
            
            var headers = new MessageHeader(
                messageId: azureServiceBusMessage.Id, 
                topic: new RoutingKey(Topic), 
                messageType: messageType, 
                source: null,
                type: "",
                timeStamp: DateTime.UtcNow,
                correlationId: azureServiceBusMessage.CorrelationId,
                replyTo: new RoutingKey(replyAddress),
                contentType: azureServiceBusMessage.ContentType,
                handledCount:handledCount, 
                dataSchema: null,
                subject: null,
                delayed: TimeSpan.Zero
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
                    out object? property))
                return MessageType.MT_EVENT;

            if (Enum.TryParse(property.ToString(), true, out MessageType messageType))
                return messageType;

            return MessageType.MT_EVENT;
        }

        private static string GetReplyAddress(IBrokeredMessageWrapper azureServiceBusMessage)
        {
            if (azureServiceBusMessage.ApplicationProperties is null ||
                !azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.ReplyToHeaderBagKey,
                    out object? property))
            {
                return string.Empty;
            }

            var replyAddress = property.ToString();

            return replyAddress ?? string.Empty;
        }

        private static int GetHandledCount(IBrokeredMessageWrapper azureServiceBusMessage)
        {
            var count = 0;
            if (azureServiceBusMessage.ApplicationProperties != null &&
                azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.HandledCountHeaderBagKey,
                    out object? property))
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
