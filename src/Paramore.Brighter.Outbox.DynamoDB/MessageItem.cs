using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    [DynamoDBTable("brighter_outbox")]
    public class MessageItem
    {
        /// <summary>
        /// The message body
        /// </summary>
        [DynamoDBProperty(typeof(MessageItemBodyConverter))]
        public byte[]? Body { get; set; }
        
        /// <summary>
        /// What is the character encoding of the body
        /// </summary>
        public string? CharacterEncoding { get; set; }

        /// <summary>
        /// What is the content type of the message
        /// </summary>
        [DynamoDBProperty]
        public string? ContentType { get; set; } 

        // <summary>
        /// The correlation id of the message
        /// </summary>
        [DynamoDBProperty]
        public string? CorrelationId { get; set; }

        /// <summary>
        /// The time at which the message was created, formatted as a string yyyy-MM-ddTHH:mm:ss.fffZ
        /// </summary>
        public string? CreatedAt { get; set; }

        /// <summary>
        /// The time at which the message was created, in ticks
        /// </summary>
        [DynamoDBProperty]
        public long? CreatedTime { get; set; }

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
        public string? DeliveredAt { get; set; }

        /// <summary>
        /// The time that the message was delivered to the broker, in ticks
        /// </summary>
        [DynamoDBGlobalSecondaryIndexRangeKey(indexName: "Delivered")]
        [DynamoDBProperty]
        public long? DeliveryTime { get; set; }

        /// <summary>
        /// A JSON object representing a dictionary of additional properties set on the message
        /// </summary>
        public string? HeaderBag { get; set; }

        /// <summary>
        /// The Id of the Message. Used as a Global Secondary Index
        /// </summary>
        [DynamoDBHashKey]
        [DynamoDBProperty]
        public string? MessageId { get; set; }

        /// <summary>
        /// The type of message i.e. MT_COMMAND, MT_EVENT etc. An enumeration rendered as a string
        /// </summary>
        public string? MessageType { get; set; }

        /// <summary>
        /// The partition key for the Kafka message
        /// </summary>
        [DynamoDBProperty]
        public string? PartitionKey { get; set; }


        /// <summary>
        /// If this is a conversation i.e. request-response, what is the reply channel
        /// </summary>
        [DynamoDBProperty]
        public string? ReplyTo { get; set; }

        /// <summary>
        /// The Topic the message was published to
        /// </summary>
        /// 
        [DynamoDBGlobalSecondaryIndexHashKey("Delivered")]
        [DynamoDBProperty]
        public string? Topic { get; set; }
        
        /// <summary>
        /// The Topic suffixed with the shard number
        /// </summary>
        [DynamoDBGlobalSecondaryIndexHashKey("Outstanding")]
        [DynamoDBProperty]
        public string? TopicShard { get; set; }
        
        [DynamoDBProperty]
        public long? ExpiresAt { get; set; }


        public MessageItem()
        {
            /*Deserialization*/
        }

        public MessageItem(Message message, int shard = 0, long? expiresAt = null)
        {
            var date = message.Header.TimeStamp == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : message.Header.TimeStamp;

            Body = message.Body.Bytes;
            var contentType = message.Header.ContentType ?? new ContentType(MediaTypeNames.Text.Plain);
            
            ContentType = contentType.ToString();
            CorrelationId = message.Header.CorrelationId.ToString();
            CharacterEncoding = message.Body.CharacterEncoding.ToString();
            CreatedAt = date.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            CreatedTime = date.Ticks;
            OutstandingCreatedTime = date.Ticks;
            DeliveryTime = null;
            HeaderBag = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
            MessageId = message.Id.ToString();
            MessageType = message.Header.MessageType.ToString();
            PartitionKey = message.Header.PartitionKey;
            ReplyTo = message.Header.ReplyTo?.Value;
            Topic = message.Header.Topic;
            TopicShard = $"{Topic}_{shard}";
            ExpiresAt = expiresAt;
        }

        public Message ConvertToMessage()
        {
            //following type may be missing on older data
            var characterEncoding = CharacterEncoding != null ? (CharacterEncoding) Enum.Parse(typeof(CharacterEncoding), CharacterEncoding) : Brighter.CharacterEncoding.UTF8;
            var correlationId = CorrelationId;
            var bag = JsonSerializer.Deserialize<Dictionary<string, object>>(HeaderBag!, JsonSerialisationOptions.Options);
            var messageId = MessageId;
            var messageType = (MessageType)Enum.Parse(typeof(MessageType), MessageType!);
            var timestamp = new DateTime(CreatedTime ?? DateTime.UtcNow.Ticks, DateTimeKind.Utc);
            var contentType = ContentType is not null ? new ContentType(ContentType) : new ContentType(MediaTypeNames.Text.Plain);
            
            var header = new MessageHeader(
                messageId: Id.Create(messageId),
                topic: Topic is not null ? new RoutingKey(Topic) :RoutingKey.Empty,
                messageType: messageType,
                timeStamp: timestamp,
                correlationId: Id.Create(correlationId),
                replyTo: ReplyTo is not null ? new RoutingKey(ReplyTo) : RoutingKey.Empty,
                contentType: contentType, 
                partitionKey: PartitionKey is not null ? new PartitionKey(PartitionKey) : Paramore.Brighter.PartitionKey.Empty);

           if (bag == null) return new Message(header, new MessageBody(Body, contentType, characterEncoding));
            
            foreach (var keyValue in bag)
                header.Bag.Add(keyValue.Key, keyValue.Value);

            return new Message(header, new MessageBody(Body, contentType, characterEncoding));
        }
    }

    public class MessageItemBodyConverter : IPropertyConverter
    {
        public DynamoDBEntry ToEntry(object? value)
        {
            byte[]? body = value as byte[];
            if (body == null) throw new ArgumentOutOfRangeException("Expected the body to be a byte array");

            DynamoDBEntry entry = new Primitive
            {
                Value = body,
                Type = DynamoDBEntryType.Binary
            };
            
            return entry;
        }

        public object FromEntry(DynamoDBEntry entry)
        {
            byte[] data = [];
            Primitive? primitive = entry as Primitive; 
            if (primitive?.Value is byte[] bytes)
                data = bytes;
            else if (primitive?.Value is string text)    //for historical data that used UTF-8 strings
                data = Encoding.UTF8.GetBytes(text);
            if (primitive == null || !(primitive.Value is string || primitive.Value is byte[]))
                throw new ArgumentOutOfRangeException("Expected Dynamo to have stored a byte array");
            
            return data;
        }
    }
}
