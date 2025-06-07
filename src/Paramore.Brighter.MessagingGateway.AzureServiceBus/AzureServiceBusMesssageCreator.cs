#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Creates a Brighter <see cref="Message"/> from an Azure Service Bus message.
/// </summary>
/// <param name="subscription">Subscription information, used to help populate the message</param>
public partial class AzureServiceBusMesssageCreator(AzureServiceBusSubscription subscription)
{
    private readonly RoutingKey _topic = subscription.RoutingKey;
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<AzureServiceBusMesssageCreator>();

    /// <summary>
    /// Maps an Azure Service Bus message to a Brighter <see cref="Message"/>.
    /// </summary>
    /// <param name="azureServiceBusMessage">The Azure Service Bus Message to map toa a a Brighter <see cref="Message"/></param>
    /// <returns></returns>
    public Message MapToBrighterMessage(IBrokeredMessageWrapper? azureServiceBusMessage)
    {
        if (azureServiceBusMessage is null)
        {
            s_logger.LogWarning("Null message received from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name); 
            return Message.FailureMessage(_topic); 
        }
        
        if (azureServiceBusMessage!.MessageBodyValue is null)
        {
            s_logger.LogWarning("Null message body received from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
        }

        var messageBody = System.Text.Encoding.Default.GetString(azureServiceBusMessage.MessageBodyValue ?? []);

        s_logger.LogDebug("Received message from topic {Topic} via subscription {ChannelName} with body {Request}", _topic, subscription.Name, messageBody);
            
        MessageType messageType = GetMessageType(azureServiceBusMessage);
        var replyAddress = GetReplyAddress(azureServiceBusMessage);
        var handledCount = GetHandledCount(azureServiceBusMessage);
        var contentType = new ContentType(azureServiceBusMessage.ContentType);
            
        // Azure is using the cloud event payload, so the user should read that info from the payload
        // https://learn.microsoft.com/en-us/azure/event-grid/cloud-event-schema
        // https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/samples/Sample11_CloudEvents.md
            
        var headers = new MessageHeader(
            messageId: azureServiceBusMessage.Id, 
            topic: new RoutingKey(_topic), 
            messageType: messageType, 
            source: null,
            type: "",
            timeStamp: DateTime.UtcNow,
            correlationId: azureServiceBusMessage.CorrelationId,
            replyTo: new RoutingKey(replyAddress),
            contentType: contentType,
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
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.MessageTypeHeaderBagKey, out object? property))
            return MessageType.MT_EVENT;

        return Enum.TryParse(property.ToString(), true, out MessageType messageType) ? messageType : MessageType.MT_EVENT;
    }

    private static string GetReplyAddress(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.ReplyToHeaderBagKey, out object? property))
        {
            return string.Empty;
        }

        var replyAddress = property.ToString();

        return replyAddress ?? string.Empty;
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
}
