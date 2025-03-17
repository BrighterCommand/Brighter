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

#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Implementation of <see cref="IAmAMessageConsumerSync"/> using Azure Service Bus for Transport.
/// </summary>
public abstract class AzureServiceBusConsumer : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
{
    protected abstract string SubscriptionName { get; }
    protected abstract ILogger Logger { get; }

    protected readonly AzureServiceBusSubscription Subscription;
    protected readonly string Topic;
    private readonly IAmAMessageProducer _messageProducer;
    protected readonly IAdministrationClientWrapper AdministrationClientWrapper;
    private readonly int _batchSize;
    protected IServiceBusReceiverWrapper? ServiceBusReceiver;
    protected readonly AzureServiceBusSubscriptionConfiguration SubscriptionConfiguration;
        
    /// <summary>
    /// Constructor for the Azure Service Bus Consumer
    /// </summary>
    /// <param name="subscription">The ASB subscription details</param>
    /// <param name="messageProducer">The producer we want to send via</param>
    /// <param name="administrationClientWrapper">The admin client for ASB</param>
    /// <param name="isAsync">Whether the consumer is async</param>
    protected AzureServiceBusConsumer(
        AzureServiceBusSubscription subscription, 
        IAmAMessageProducer messageProducer,
        IAdministrationClientWrapper administrationClientWrapper,
        bool isAsync = false
    )
    {
        Subscription = subscription;
        Topic = subscription.RoutingKey;
        _batchSize = subscription.BufferSize;
        SubscriptionConfiguration = subscription.Configuration ?? new AzureServiceBusSubscriptionConfiguration();
        _messageProducer = messageProducer;
        AdministrationClientWrapper = administrationClientWrapper;
    }
        
    /// <summary>
    /// Dispose of the Consumer.
    /// </summary>
    public void Dispose()
    {
        ServiceBusReceiver?.Close();
        GC.SuppressFinalize(this);
    }
        
    public async ValueTask DisposeAsync()
    {
        if (ServiceBusReceiver is not null) await ServiceBusReceiver.CloseAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Acknowledges the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Acknowledge(Message message) => BrighterAsyncContext.Run(async() => await AcknowledgeAsync(message));

    /// <summary>
    /// Acknowledges the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the acknowledge operation</param>
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
    {
        try
        {
            await EnsureChannelAsync();
            var lockToken = message.Header.Bag[ASBConstants.LockTokenHeaderBagKey].ToString();

            if (string.IsNullOrEmpty(lockToken))
                throw new Exception($"LockToken for message with id {message.Id} is null or empty");
            Logger.LogDebug("Acknowledging Message with Id {Id} Lock Token : {LockToken}", message.Id,
                lockToken);
                
            if(ServiceBusReceiver == null)
                await GetMessageReceiverProviderAsync();

            await ServiceBusReceiver!.CompleteAsync(lockToken);
                
            if (SubscriptionConfiguration.RequireSession)
                if (ServiceBusReceiver is not null) await ServiceBusReceiver.CloseAsync();
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
    /// Purges the specified queue name.
    /// </summary>
    public void Purge() => BrighterAsyncContext.Run(async () => await PurgeAsync());
        
    /// <summary>
    /// Purges the specified queue name.
    /// </summary>
    public abstract Task PurgeAsync(CancellationToken cancellationToken = default(CancellationToken));
        
    /// <summary>
    /// Receives the specified queue name.
    /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge
    /// the processing of those messages or requeue them.
    /// Used by a <see cref="Channel"/> to provide access to a third-party message queue.
    /// Sync over async
    /// </summary>
    /// <param name="timeOut">The timeout for a message being available. Defaults to 300ms.</param>
    /// <returns>Message.</returns>
    public Message[] Receive(TimeSpan? timeOut = null) => BrighterAsyncContext.Run(async () => await ReceiveAsync(timeOut));
        
    /// <summary>
    /// Receives the specified queue name.
    /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge
    /// the processing of those messages or requeue them.
    /// Used by a <see cref="Channel"/> to provide access to a third-party message queue.
    /// </summary>
    /// <param name="timeOut">The timeout for a message being available. Defaults to 300ms.</param>
    /// <param name="cancellationToken">Cancel the receive</param>
    /// <returns>Message.</returns>
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        Logger.LogDebug(
            "Preparing to retrieve next message(s) from topic {Topic} via subscription {ChannelName} with timeout {Timeout} and batch size {BatchSize}",
            Topic, SubscriptionName, timeOut, _batchSize);

        IEnumerable<IBrokeredMessageWrapper> messages;
        await EnsureChannelAsync();

        var messagesToReturn = new List<Message>();

        try
        {
            if (SubscriptionConfiguration.RequireSession || ServiceBusReceiver == null)
            {
                await GetMessageReceiverProviderAsync();
                if (ServiceBusReceiver == null)
                {
                    Logger.LogInformation("Message Gateway: Could not get a lock on a session for {TopicName}", Topic);
                    return messagesToReturn.ToArray();   
                }
            }

            timeOut ??= TimeSpan.FromMilliseconds(300);

            messages = await ServiceBusReceiver.ReceiveAsync(_batchSize, timeOut.Value);
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
                await GetMessageReceiverProviderAsync();

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
    /// Rejects the specified message.
    /// Sync over Async
    /// </summary>
    /// <param name="message">The message.</param>
    public void Reject(Message message) => BrighterAsyncContext.Run(async () => await RejectAsync(message));

    /// <summary>
    /// Rejects the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancel the rejection</param>
    public async Task RejectAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
    {
        try
        {
            await EnsureChannelAsync();
            var lockToken = message.Header.Bag[ASBConstants.LockTokenHeaderBagKey].ToString();

            if (string.IsNullOrEmpty(lockToken))
                throw new Exception($"LockToken for message with id {message.Id} is null or empty");
            Logger.LogDebug("Dead Lettering Message with Id {Id} Lock Token : {LockToken}", message.Id, lockToken);

            if(ServiceBusReceiver == null)
                await GetMessageReceiverProviderAsync();

            await ServiceBusReceiver!.DeadLetterAsync(lockToken);
            if (SubscriptionConfiguration.RequireSession)
                if (ServiceBusReceiver is not null) await ServiceBusReceiver.CloseAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error Dead Lettering message with id {Id}", message.Id);
            throw;
        }
    }

    /// <summary>
    /// Requeues the specified message.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="delay">Delay to the delivery of the message. 0 is no delay. Defaults to 0.</param>
    /// <returns>True if the message should be acked, false otherwise</returns>
    public bool Requeue(Message message, TimeSpan? delay = null) => BrighterAsyncContext.Run(async () => await RequeueAsync(message, delay));

    /// <summary>
    /// Requeues the specified message.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="delay">Delay to the delivery of the message. 0 is no delay. Defaults to 0.</param>
    /// <param name="cancellationToken">Cancel the requeue ioperation</param>
    /// <returns>True if the message should be acked, false otherwise</returns>
    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        var topic = message.Header.Topic;
        delay ??= TimeSpan.Zero;

        Logger.LogInformation("Requeuing message with topic {Topic} and id {Id}", topic, message.Id);

        var messageProducerAsync = _messageProducer as IAmAMessageProducerAsync;
            
        if (messageProducerAsync  is null)
        {
            throw new ChannelFailureException("Message Producer is not of type IAmAMessageProducerSync");    
        }
            
        if (delay.Value > TimeSpan.Zero)
        {
            await messageProducerAsync.SendWithDelayAsync(message, delay.Value, cancellationToken);
        }
        else
        {
            await messageProducerAsync.SendAsync(message, cancellationToken);
        }
            
        await AcknowledgeAsync(message, cancellationToken);

        return true;
    }

    protected abstract Task GetMessageReceiverProviderAsync();

    private Message MapToBrighterMessage(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (azureServiceBusMessage.MessageBodyValue is null)
        {
            Logger.LogWarning(
                "Null message body received from topic {Topic} via subscription {ChannelName}",
                Topic, SubscriptionName);
        }
        
        var messageType = GetMessageType(azureServiceBusMessage);
        var replyAddress = GetReplyAddress(azureServiceBusMessage);
        var handledCount = GetHandledCount(azureServiceBusMessage);
        var id = azureServiceBusMessage.Id;
        var contentType = azureServiceBusMessage.ContentType;
        var source = new Uri(MessageHeader.DefaultSource);
        var type = MessageHeader.DefaultType;
        var timestamp = DateTimeOffset.UtcNow;
        string? subject = null;
        Uri? dataSchema = null;
        byte[] body;
        
        try
        {
            var cloudEvents = CloudEvent.Parse(new BinaryData(azureServiceBusMessage.MessageBodyValue!));
            if (cloudEvents != null)
            {
                id = cloudEvents.Id;
                type = cloudEvents.Type;
                subject = cloudEvents.Subject;
                contentType = cloudEvents.DataContentType;
                timestamp = cloudEvents.Time ?? DateTimeOffset.UtcNow;
            
                if (Uri.TryCreate(cloudEvents.Source, UriKind.RelativeOrAbsolute, out var tmp))
                {
                    source = tmp;
                }

                if (Uri.TryCreate(cloudEvents.DataSchema, UriKind.RelativeOrAbsolute, out tmp))
                {
                    dataSchema = tmp;
                }
                
                body = cloudEvents.Data!.ToArray();
            }
            else
            {
                body = azureServiceBusMessage.MessageBodyValue!;
            }
        }
        catch (ArgumentException) 
        {
            // Case it wasn't send using cloud event we are going to get ArgumentException 
            body = azureServiceBusMessage.MessageBodyValue!;
            var messageBody = System.Text.Encoding.Default.GetString(azureServiceBusMessage.MessageBodyValue ?? []);

            Logger.LogDebug("Received message from topic {Topic} via subscription {ChannelName} with body {Request}",
                Topic, SubscriptionName, messageBody);
        }
        
        var headers = new MessageHeader(
            messageId: id,
            topic: new RoutingKey(Topic),
            messageType: messageType,
            source: source,
            type: type,
            timeStamp: timestamp,
            correlationId: azureServiceBusMessage.CorrelationId,
            replyTo: replyAddress,
            contentType: contentType!,
            handledCount: handledCount,
            dataSchema: dataSchema,
            subject: subject,
            delayed: TimeSpan.Zero
        );
        
        headers.Bag.Add(ASBConstants.LockTokenHeaderBagKey, azureServiceBusMessage.LockToken);
        
        foreach (var property in azureServiceBusMessage.ApplicationProperties)
        {
            headers.Bag.Add(property.Key, property.Value);
        }
        
        var message = new Message(headers, new MessageBody(body));
        return message;
    }

    private static MessageType GetMessageType(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.MessageTypeHeaderBagKey,
                out object? property))
            return MessageType.MT_EVENT;

        return Enum.TryParse(property.ToString(), true, out MessageType messageType) ? messageType : MessageType.MT_EVENT;
    }

    private static RoutingKey GetReplyAddress(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.ReplyToHeaderBagKey,
                out object? property))
        {
            return RoutingKey.Empty;
        }

        var replyAddress = property.ToString();

        return new RoutingKey(replyAddress ?? string.Empty);
    }

    private static int GetHandledCount(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        var count = 0;
        if (azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.HandledCountHeaderBagKey,
                out object? property))
        {
            int.TryParse(property.ToString(), out count);
        }

        return count;
    }

    protected abstract Task EnsureChannelAsync();

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
