using System;
using System.Collections.Generic;
using System.Text.Json;
using Amazon.DynamoDBv2.DataModel;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    [DynamoDBTable("brighter_outbox")]
    public class MessageItem
    {
        /// <summary>
        /// The message body
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// The time at which the message was created, formatted as a string yyyy-MM-dd
        /// </summary>
        public string CreatedAt { get; set; }

        /// <summary>
        /// The time at which the message was created, in ticks
        /// </summary>
        [DynamoDBGlobalSecondaryIndexRangeKey(indexName: "Outstanding")]
        [DynamoDBProperty]
        public long CreatedTime { get; set; }

        /// <summary>
        /// The time at which the message was delivered, formatted as a string yyyy-MM-dd
        /// </summary>
        [DynamoDBProperty]
        public string DeliveredAt { get; set; }

        /// <summary>
        /// The time that the message was delivered to the broker, in ticks
        /// </summary>
        [DynamoDBGlobalSecondaryIndexRangeKey(indexName: "Delivered")]
        [DynamoDBProperty]
        public long DeliveryTime { get; set; }

        /// <summary>
        /// A JSON object representing a dictionary of additional properties set on the message
        /// </summary>
        public string HeaderBag { get; set; }

        /// <summary>
        /// The Id of the Message. Used as a Global Secondary Index
        /// </summary>
        [DynamoDBHashKey]
        [DynamoDBProperty]
        public string MessageId { get; set; }

        /// <summary>
        /// The type of message i.e. MT_COMMAND, MT_EVENT etc. An enumeration rendered as a string
        /// </summary>
        public string MessageType { get; set; }

        /// <summary>
        /// The Topic the message was published to
        /// </summary>
        [DynamoDBGlobalSecondaryIndexHashKey("Delivered", "Outstanding")]
        [DynamoDBProperty]
        public string Topic { get; set; }

        /// <summary>
        /// The correlation id of the message
        /// </summary>
        [DynamoDBProperty]
        public string CorrelationId { get; set; }

        /// <summary>
        /// If this is a conversation i.e. request-response, what is the reply channel
        /// </summary>
        [DynamoDBProperty]
        public string ReplyTo { get; set; }
        
        /// <summary>
        /// The partition key for the Kafka message
        /// </summary>
        [DynamoDBProperty]
        public string PartitionKey { get; set; }
        
        /// <summary>
        /// What is the content type of the message
        /// </summary>
        [DynamoDBProperty]
        public string ContentType { get; set; }
        
        public MessageItem()
        {
            /*Deserialization*/
        }

        public MessageItem(Message message)
        {
            var date = message.Header.TimeStamp == DateTime.MinValue ? DateTime.UtcNow : message.Header.TimeStamp;

            CreatedTime = date.Ticks;
            MessageId = message.Id.ToString();
            Topic = message.Header.Topic;
            MessageType = message.Header.MessageType.ToString();
            CorrelationId = message.Header.CorrelationId.ToString();
            ReplyTo = message.Header.ReplyTo;
            ContentType = message.Header.ContentType;
            CreatedAt = $"{date}";
            HeaderBag = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            PartitionKey = message.Header.PartitionKey;
            Body = message.Body.Value;
            DeliveryTime = 0;
        }

        public Message ConvertToMessage()
        {
            var messageId = Guid.Parse(MessageId);
            var messageType = (MessageType)Enum.Parse(typeof(MessageType), MessageType);
            var timestamp = DateTime.Parse(CreatedAt);
            var correlationId = Guid.Parse(CorrelationId);
            var bag = JsonSerializer.Deserialize<Dictionary<string, object>>(HeaderBag, JsonSerialisationOptions.Options);

            var header = new MessageHeader(
                messageId:messageId, 
                topic: Topic, 
                messageType: messageType, 
                timeStamp: timestamp,
                correlationId: correlationId,
                replyTo: ReplyTo,
                partitionKey: PartitionKey,
                contentType: ContentType);

            foreach (var key in bag.Keys)
            {
                header.Bag.Add(key, bag[key]);
            }

            var body = new MessageBody(Body);

            return new Message(header, body);
        }

        public void MarkMessageDelivered(DateTime deliveredAt)
        {
            DeliveryTime = deliveredAt.Ticks;
            DeliveredAt = $"{deliveredAt:yyyy-MM-dd}";
        }
    }
}
