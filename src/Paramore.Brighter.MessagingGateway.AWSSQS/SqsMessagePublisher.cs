using System;
using System.Collections.Generic;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Newtonsoft.Json;

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

        public void Publish(Message message)
        {
            var messageString = JsonConvert.SerializeObject(message.Body);
            var publishRequest = new PublishRequest(_topicArn, messageString);

            var messageAttributes = new Dictionary<string, MessageAttributeValue>();
            messageAttributes.Add(HeaderNames.Id, new MessageAttributeValue{StringValue = Convert.ToString(message.Header.Id), DataType = "String"});
            messageAttributes.Add(HeaderNames.Topic, new MessageAttributeValue{StringValue = _topicArn, DataType = "String"});
            messageAttributes.Add(HeaderNames.ContentType, new MessageAttributeValue {StringValue = message.Header.ContentType, DataType = "String"});
            messageAttributes.Add(HeaderNames.CorrelationId, new MessageAttributeValue{StringValue = Convert.ToString(message.Header.CorrelationId), DataType = "String"});
            messageAttributes.Add(HeaderNames.HandledCount, new MessageAttributeValue {StringValue = Convert.ToString(message.Header.HandledCount), DataType = "String"});
            messageAttributes.Add(HeaderNames.MessageType, new MessageAttributeValue{StringValue = message.Header.MessageType.ToString(), DataType = "String"});
            messageAttributes.Add(HeaderNames.Timestamp, new MessageAttributeValue{StringValue = Convert.ToString(message.Header.TimeStamp), DataType = "String"});
            messageAttributes.Add(HeaderNames.ReplyTo, new MessageAttributeValue{StringValue = Convert.ToString(message.Header.ReplyTo), DataType = "String"});
             
            //we can set up to 10 attributes; we have set 6 above, so use a single JSON object as the bag
            var bagJson = JsonConvert.SerializeObject(message.Header.Bag.Keys);

            messageAttributes.Add(HeaderNames.Bag, new MessageAttributeValue{StringValue = Convert.ToString(bagJson), DataType = "String"});
            publishRequest.MessageAttributes = messageAttributes;
            
            
            _client.PublishAsync(publishRequest).Wait();
        }
    }
}
