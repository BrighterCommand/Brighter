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
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Creates a Brighter <see cref="Message"/> from an Azure Service Bus message.
/// </summary>
/// <param name="subscription">Subscription information, used to help populate the message</param>
public class AzureServiceBusMesssageCreator(AzureServiceBusSubscription subscription)
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
            
        //TODO: Switch these to use the option type HeaderResult<T> for consistency with the rest of the codebase.
        MessageType messageType = GetMessageType(azureServiceBusMessage);
        var replyAddress = GetReplyAddress(azureServiceBusMessage);
        var handledCount = GetHandledCount(azureServiceBusMessage);
        var contentType = GetContentType(azureServiceBusMessage);
        var source = GetSource(azureServiceBusMessage);
        var type = GetCloudEventsType(azureServiceBusMessage);
        var time = GetCloudEventsTime(azureServiceBusMessage);
        var dataSchema = GetCloudEventsDataSchema(azureServiceBusMessage);
        var subject = GetCloudEventsSubject(azureServiceBusMessage);
        var traceParent = GetTraceParent(azureServiceBusMessage);
        var traceState = GetTraceState(azureServiceBusMessage);
        var baggage = GetBaggage(azureServiceBusMessage);
        var partitionKey = GetPartitionKey(azureServiceBusMessage);
            
        // TODO: We only support  a header based approach to Cloud Events at the moment, so we don't
        // have support here for Cloud Events JSON as the body. We should probably support that as well in the future.
        // https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/samples/Sample11_CloudEvents.md
            
        var headers = new MessageHeader(
            messageId: azureServiceBusMessage.Id, 
            topic: new RoutingKey(_topic), 
            messageType: messageType, 
            source: source,
            type: type,
            timeStamp: time,
            correlationId: azureServiceBusMessage.CorrelationId,
            replyTo: new RoutingKey(replyAddress),
            contentType: contentType,
            handledCount:handledCount, 
            dataSchema: dataSchema,
            subject: subject,
            delayed: TimeSpan.Zero,
            traceParent: traceParent,
            traceState: traceState,
            baggage: baggage,
            partitionKey: partitionKey
        );

        headers.Bag.Add(ASBConstants.LockTokenHeaderBagKey, azureServiceBusMessage.LockToken);
            
        foreach (var property in azureServiceBusMessage.ApplicationProperties)
        {
            headers.Bag.Add(property.Key, property.Value);
        }
            
        var message = new Message(headers, new MessageBody(messageBody));
        return message;
    }

    private Baggage GetBaggage(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.Baggage, out object? property))
        {
            s_logger.LogWarning("No baggage found in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return new Baggage();
        }
        
        var baggageString = property.ToString() ?? string.Empty;

        var baggage = new Baggage();
        baggage.LoadBaggage(baggageString);
        return baggage;
    }

    private Uri GetCloudEventsDataSchema(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        var defaultSchemaUri = new Uri("http://goparamore.io"); // Default schema URI
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.CloudEventsSchema, out object? property))
        {
            s_logger.LogWarning("No Cloud Events data schema found in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return defaultSchemaUri;
        }

        var dataSchema = property.ToString() ;
        
        if (string.IsNullOrEmpty(dataSchema))
        {
            s_logger.LogWarning("Empty Cloud Events data schema in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return defaultSchemaUri;
        }

        return new Uri(dataSchema);
    }

    private string GetCloudEventsSubject(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.CloudEventsSubject, out object? property))
        {
            s_logger.LogWarning("No Cloud Events subject found in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return string.Empty;
        }

        var subject = property.ToString() ?? string.Empty;

        return subject;
    }

    private DateTimeOffset GetCloudEventsTime(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.CloudEventsTime, out object? property))
        {
            s_logger.LogWarning("No Cloud Events time found in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return DateTimeOffset.UtcNow;
        }
        
        var time = property.ToString() ?? string.Empty;

        if (!string.IsNullOrEmpty(time) && DateTimeOffset.TryParse(time, out DateTimeOffset parsedTime))
        {
            return parsedTime;
        }

        s_logger.LogWarning("Invalid Cloud Events time format in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
        return DateTimeOffset.UtcNow;
    }

    private PartitionKey GetPartitionKey(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.CloudEventsParitionKey, out object? property))
        {
            s_logger.LogWarning("No Cloud Events partition key found in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return PartitionKey.Empty;
        }

        return new PartitionKey(property.ToString() ?? string.Empty);
    }

    private CloudEventsType GetCloudEventsType(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.CloudEventsType, out object? property))
        {
            s_logger.LogWarning("No Cloud Events type found in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return CloudEventsType.Empty;
        }

        return new CloudEventsType(property.ToString() ?? string.Empty);
    }

    private static ContentType GetContentType(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!string.IsNullOrEmpty(azureServiceBusMessage.ContentType))
            return new ContentType(azureServiceBusMessage.ContentType);

        return new ContentType(MediaTypeNames.Text.Plain);
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

        var replyAddress = property.ToString() ?? string.Empty;

        return replyAddress ;
    }

    private Uri GetSource(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        var defaultSourceUri = new Uri("http://goparamore.io"); // Default source URI
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.CloudEventsSource, out object? property))
        {
            s_logger.LogWarning("No source found in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return defaultSourceUri;
        }

        if (property is not string sourceString || string.IsNullOrEmpty(sourceString))
        {
            s_logger.LogWarning("Empty or invalid source in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return defaultSourceUri;
        }
        
        var source = property.ToString();

        return new Uri(source!);
    }
    
   private TraceParent GetTraceParent(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.TraceParent, out object?property))
        {
            s_logger.LogWarning("No trace parent found in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return new TraceParent(string.Empty);
        } 
        
        var traceParentString = property.ToString() ?? string.Empty;
        
        return new TraceParent(traceParentString);
    }

    private TraceState GetTraceState(IBrokeredMessageWrapper azureServiceBusMessage)
    {
        if (!azureServiceBusMessage.ApplicationProperties.TryGetValue(ASBConstants.TraceState, out object? property))
        {
            s_logger.LogWarning("No trace state found in message from topic {Topic} via subscription {SubscriptionName}", _topic, subscription.Name);
            return new TraceState(string.Empty);
        } 
        
        var traceStateString = property.ToString() ?? string.Empty;
        
        return new TraceState(traceStateString);
        
    }
}
