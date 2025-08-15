using System.Linq;
using System.Net.Mime;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.Extensions;

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

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Maps a Brighter <see cref="Message"/> to an Azure Service Bus <see cref="ServiceBusMessage"/>.
/// </summary>
public class AzureServiceBusMessagePublisher
{
    /// <summary>
    /// Map a Brighter <see cref="Message"/> to an Azure Service Bus <see cref="ServiceBusMessage"/>.
    /// </summary>
    /// <param name="message">The Azure Service Bus <see cref="ServiceBusMessage"/> to map to a  Brighter <see cref="Message"/></param>
    /// <returns></returns>
    public static ServiceBusMessage ConvertToServiceBusMessage(Message message)
    {
        var azureServiceBusMessage = new ServiceBusMessage(message.Body.Value);
        
        AddBrighterHeaders(message, azureServiceBusMessage);
        AddCloudEventHeaders(message, azureServiceBusMessage);
        AddOtelHeaders(message, azureServiceBusMessage);

        return azureServiceBusMessage;
    }

    private static void AddBrighterHeaders(Message message, ServiceBusMessage azureServiceBusMessage)
    {
        //Add Brighter message properties to the Azure Service Bus message
        azureServiceBusMessage.MessageId = message.Header.MessageId;
        azureServiceBusMessage.ContentType = message.Header.ContentType is not null ? message.Header.ContentType.ToString() : MediaTypeNames.Text.Plain;
        if(!string.IsNullOrEmpty(message.Header.CorrelationId))
            azureServiceBusMessage.CorrelationId = message.Header.CorrelationId;
        if (!string.IsNullOrEmpty(message.Header.ReplyTo!))
            azureServiceBusMessage.ReplyTo = message.Header.ReplyTo?.Value;
        if (message.Header.Bag.TryGetValue(ASBConstants.SessionIdKey, out object? value))
            azureServiceBusMessage.SessionId = value.ToString();

        foreach (var header in message.Header.Bag.Where(h => !ASBConstants.ReservedHeaders.Contains(h.Key)))
        {
            azureServiceBusMessage.ApplicationProperties[header.Key] = header.Value;
        }
            
        azureServiceBusMessage.ApplicationProperties[ASBConstants.MessageTypeHeaderBagKey] = message.Header.MessageType.ToString();
        azureServiceBusMessage.ApplicationProperties[ASBConstants.HandledCountHeaderBagKey] = message.Header.HandledCount;
        azureServiceBusMessage.ApplicationProperties[ASBConstants.ReplyToHeaderBagKey] = message.Header.ReplyTo?.Value;
        
   }
    
    private static void AddCloudEventHeaders(Message message, ServiceBusMessage azureServiceBusMessage)
    {
        //required Cloud Event headers
        azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventsSource] = message.Header.Source?.ToString() ?? string.Empty;
        azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventsId] = message.Id.ToString();
        azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventsSpecVersion] = message.Header.SpecVersion;
        azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventsType] = message.Header.Type.Value;

        //optional Cloud Event headers
        if (message.Header.ContentType is not null)
            azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventsContentType] = message.Header.ContentType.ToString();
       
        if(message.Header.DataSchema is not null)
            azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventsSchema] = message.Header.DataSchema;
        
        if (!string.IsNullOrEmpty(message.Header.Subject))
            azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventsSubject] = message.Header.Subject;
       
        if (message.Header.TimeStamp != default)
            azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventsTime] = message.Header.TimeStamp.ToRfc3339();
        
        //extension Cloud Event headers
        if (message.Header.DataRef is not null)
            azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventDataRef] = message.Header.DataRef;
        
        azureServiceBusMessage.ApplicationProperties[ASBConstants.CloudEventsParitionKey] = message.Header.PartitionKey.Value;
    }
    
    private static void AddOtelHeaders(Message message, ServiceBusMessage azureServiceBusMessage)
    {
        if(message.Header.TraceParent is not null)
            azureServiceBusMessage.ApplicationProperties[ASBConstants.OtelTraceParent] = message.Header.TraceParent.Value;
        
        if(message.Header.TraceState is not null)
            azureServiceBusMessage.ApplicationProperties[ASBConstants.OtelTraceState] = message.Header.TraceState.Value;
        
        azureServiceBusMessage.ApplicationProperties[ASBConstants.Baggage] = message.Header.Baggage.ToString();
    }
}
