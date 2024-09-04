using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    [DynamoDBTable("brighter_outbox")]
    public class MessageItem
    {
        /// <summary>
        /// The message body
        /// </summary>
        [DynamoDBProperty(typeof(MessageItemBodyConverter))]
        public byte[] Body { get; set; }
        
        /// <summary>
        /// What is the character encoding of the body
        /// </summary>
        public string CharacterEncoding { get; set; }

        /// <summary>
        /// What is the content type of the message
        /// </summary>
        [DynamoDBProperty]
        public string ContentType { get; set; } 

        // <summary>
        /// The correlation id of the message
        /// </summary>
        [DynamoDBProperty]
        public string CorrelationId { get; set; }

        /// <summary>
        /// The time at which the message was created, formatted as a string yyyy-MM-dd
        /// </summary>
        public string CreatedAt { get; set; }

        /// <summary>
        /// The time at which the message was created, in ticks
        /// </summary>
        [DynamoDBProperty]
        public long CreatedTime { get; set; }

        /// <summary>
        /// The time at which the message was created, in ticks. Null if the message has been dispatched.
        /// </summary>
        [DynamoDBGlobalSecondaryIndexRangeKey(indexName: "Outstanding")]
        [DynamoDBProperty]
        public long? OutstandingCreatedTime { get; set; }

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
        /// The partition key for the Kafka message
        /// </summary>
        [DynamoDBProperty]
        public string PartitionKey { get; set; }


        /// <summary>
        /// If this is a conversation i.e. request-response, what is the reply channel
        /// </summary>
        [DynamoDBProperty]
        public string ReplyTo { get; set; }

        /// <summary>
        /// The Topic the message was published to
        /// </summary>
        /// 
        [DynamoDBGlobalSecondaryIndexHashKey("Delivered")]
        [DynamoDBProperty]
        public string Topic { get; set; }
        
        /// <summary>
        /// The Topic suffixed with the shard number
        /// </summary>
        [DynamoDBGlobalSecondaryIndexHashKey("Outstanding")]
        [DynamoDBProperty]
        public string TopicShard { get; set; }
        
        [DynamoDBProperty]
        public long? ExpiresAt { get; set; }


        public MessageItem()
        {
            /*Deserialization*/
        }

        public MessageItem(Message message, int shard = 0, long? expiresAt = null)
        {
            var date = message.Header.TimeStamp == DateTime.MinValue ? DateTime.UtcNow : message.Header.TimeStamp;

            Body = message.Body.Bytes;
            ContentType = message.Header.ContentType;
            CorrelationId = message.Header.CorrelationId.ToString();
            CharacterEncoding = message.Body.CharacterEncoding.ToString();
            CreatedAt = $"{date}";
            CreatedTime = date.Ticks;
            OutstandingCreatedTime = date.Ticks;
            DeliveryTime = 0;
            HeaderBag = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            MessageId = message.Id.ToString();
            MessageType = message.Header.MessageType.ToString();
            PartitionKey = message.Header.PartitionKey;
            ReplyTo = message.Header.ReplyTo;
            Topic = message.Header.Topic;
            TopicShard = $"{Topic}_{shard}";
            ExpiresAt = expiresAt;
        }

        public Message ConvertToMessage()
        {
            //following type may be missing on older data
            var characterEncoding = CharacterEncoding != null ? (CharacterEncoding) Enum.Parse(typeof(CharacterEncoding), CharacterEncoding) : Brighter.CharacterEncoding.UTF8;
            var correlationId = Guid.Parse(CorrelationId);
            var bag = JsonSerializer.Deserialize<Dictionary<string, object>>(HeaderBag,
                JsonSerialisationOptions.Options);
            var messageId = Guid.Parse(MessageId);
            var messageType = (MessageType)Enum.Parse(typeof(MessageType), MessageType);
            var timestamp = DateTime.Parse(CreatedAt);

            var header = new MessageHeader(
                messageId: messageId,
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

            var body = new MessageBody(Body, ContentType, characterEncoding);

            return new Message(header, body);
        }
    }

    public class MessageItemBodyConverter : IPropertyConverter
    {
        public DynamoDBEntry ToEntry(object value)
        {
            byte[] body = value as byte[];
            if (body == null)
                throw new ArgumentOutOfRangeException("Expected the body to be a byte array");

            DynamoDBEntry entry = new Primitive
            {
                Value = body,
                Type = DynamoDBEntryType.Binary

            };

            return entry;
        }

        public object FromEntry(DynamoDBEntry entry)
        {
            byte[] data = Array.Empty<byte>();
            Primitive primitive = entry as Primitive;
            if (primitive?.Value is byte[] bytes)
                data = bytes;
            if (primitive?.Value is string text)    //for historical data that used UTF-8 strings
                data = Encoding.UTF8.GetBytes(text);
            if (primitive == null || !(primitive.Value is string || primitive.Value is byte[]))
                throw new ArgumentOutOfRangeException("Expected Dynamo to have stored a byte array");

            return data;
        }
    }
}
