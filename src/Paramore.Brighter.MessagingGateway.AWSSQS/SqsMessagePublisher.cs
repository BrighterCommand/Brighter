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
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SqsMessagePublisher
    {
        private readonly string _topicArn;
        private readonly AmazonSimpleNotificationServiceClient _client;

        public SqsMessagePublisher(string topicArn, AmazonSimpleNotificationServiceClient client)
        {
            _topicArn = topicArn;
            _client = client;
        }

        public async Task<string> PublishAsync(Message message)
        {
            var messageString = message.Body.Value;
            var subject = GetSubject(message);
            var publishRequest = new PublishRequest(_topicArn, messageString, subject);

            var messageAttributes = new Dictionary<string, MessageAttributeValue>();
            messageAttributes.Add(HeaderNames.Id, new MessageAttributeValue{StringValue = Convert.ToString(message.Header.Id), DataType = "String"});
            messageAttributes.Add(HeaderNames.Topic, new MessageAttributeValue{StringValue = _topicArn, DataType = "String"});
            messageAttributes.Add(HeaderNames.ContentType, new MessageAttributeValue {StringValue = message.Header.ContentType, DataType = "String"});
            messageAttributes.Add(HeaderNames.CorrelationId, new MessageAttributeValue{StringValue = Convert.ToString(message.Header.CorrelationId), DataType = "String"});
            messageAttributes.Add(HeaderNames.HandledCount, new MessageAttributeValue {StringValue = Convert.ToString(message.Header.HandledCount), DataType = "String"});
            messageAttributes.Add(HeaderNames.MessageType, new MessageAttributeValue{StringValue = message.Header.MessageType.ToString(), DataType = "String"});
            messageAttributes.Add(HeaderNames.Timestamp, new MessageAttributeValue{StringValue = Convert.ToString(message.Header.TimeStamp), DataType = "String"});
            if (!string.IsNullOrEmpty(message.Header.ReplyTo))
                messageAttributes.Add(HeaderNames.ReplyTo, new MessageAttributeValue{StringValue = Convert.ToString(message.Header.ReplyTo), DataType = "String"});
             
            //we can set up to 10 attributes; we have set 6 above, so use a single JSON object as the bag
            var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);

            messageAttributes.Add(HeaderNames.Bag, new MessageAttributeValue{StringValue = Convert.ToString(bagJson), DataType = "String"});
            publishRequest.MessageAttributes = messageAttributes;
            
            
             var response = await _client.PublishAsync(publishRequest);
             if (response.HttpStatusCode == System.Net.HttpStatusCode.OK || response.HttpStatusCode == System.Net.HttpStatusCode.Created || response.HttpStatusCode == System.Net.HttpStatusCode.Accepted)
             {
                 return response.MessageId;
             }

             return null;
        }

        private static string GetSubject(Message message)
        {
            var subjectExists = message.Header.Bag.TryGetValue("Subject", out var subject);
            if (subjectExists)
            {
                message.Header.Bag.Remove("Subject");
            }

            return subject?.ToString();
        }

        public string Publish(Message message)
        {
            return PublishAsync(message).GetAwaiter().GetResult();
        }
    }
}
