﻿#region Licence

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
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

public class SnsMessagePublisher
{
    private readonly string _topicArn;
    private readonly AmazonSimpleNotificationServiceClient _client;
    private readonly SnsSqsType _snsSqsType;
    private readonly bool _deduplication;

    public SnsMessagePublisher(string topicArn, AmazonSimpleNotificationServiceClient client, SnsSqsType snsSqsType,
        bool deduplication)
    {
        _topicArn = topicArn;
        _client = client;
        _snsSqsType = snsSqsType;
        _deduplication = deduplication;
    }

    public async Task<string?> PublishAsync(Message message)
    {
        var messageString = message.Body.Value;
        var publishRequest = new PublishRequest(_topicArn, messageString, message.Header.Subject);

        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            [HeaderNames.Id] =
                new() { StringValue = Convert.ToString(message.Header.MessageId), DataType = "String" },
            [HeaderNames.Topic] = new() { StringValue = _topicArn, DataType = "String" },
            [HeaderNames.ContentType] = new() { StringValue = message.Header.ContentType, DataType = "String" },
            [HeaderNames.CorrelationId] =
                new() { StringValue = Convert.ToString(message.Header.CorrelationId), DataType = "String" },
            [HeaderNames.HandledCount] =
                new() { StringValue = Convert.ToString(message.Header.HandledCount), DataType = "String" },
            [HeaderNames.MessageType] =
                new() { StringValue = message.Header.MessageType.ToString(), DataType = "String" },
            [HeaderNames.Timestamp] = new()
            {
                StringValue = Convert.ToString(message.Header.TimeStamp), DataType = "String"
            }
        };
        
        if (_snsSqsType == SnsSqsType.Fifo)
        {
            publishRequest.MessageGroupId = message.Header.PartitionKey;
            if (_deduplication && message.Header.Bag.TryGetValue(HeaderNames.DeduplicationId, out var deduplicationId))
            {
                publishRequest.MessageDeduplicationId = (string)deduplicationId;
            }
        }

        if (!string.IsNullOrEmpty(message.Header.ReplyTo))
        {
            messageAttributes.Add(HeaderNames.ReplyTo,
                new MessageAttributeValue
                {
                    StringValue = Convert.ToString(message.Header.ReplyTo), DataType = "String"
                });
        }


        //we can set up to 10 attributes; we have set 6 above, so use a single JSON object as the bag
        var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
        messageAttributes[HeaderNames.Bag] = new() { StringValue = Convert.ToString(bagJson), DataType = "String" };
        publishRequest.MessageAttributes = messageAttributes;

        var response = await _client.PublishAsync(publishRequest);
        if (response.HttpStatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Created
            or System.Net.HttpStatusCode.Accepted)
        {
            return response.MessageId;
        }

        return null;
    }
}
