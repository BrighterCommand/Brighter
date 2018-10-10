using System;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessagingGateway.AWSSQS.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    internal class SqsMessageCreator
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<SqsMessageCreator>);

        public Message CreateMessage(Amazon.SQS.Model.Message sqsMessage)
        {
            var topic = HeaderResult<string>.Empty();
            var messageId = HeaderResult<Guid>.Empty();
            var contentType = HeaderResult<string>.Empty();
            var correlationId = HeaderResult<Guid>.Empty();
            var handledCount = HeaderResult<int>.Empty();
            var messageType = HeaderResult<MessageType>.Empty();
            var timeStamp = HeaderResult<DateTime>.Empty();
            var receiptHandle = HeaderResult<string>.Empty();
            

            Message message;
            try
            {
                topic = ReadTopic(sqsMessage);
                messageId = ReadMessageId(sqsMessage);
                contentType = ReadContentType(sqsMessage);
                correlationId = ReadCorrelationid(sqsMessage);
                handledCount = ReadHandledCount(sqsMessage);
                messageType = ReadMessageType(sqsMessage);
                timeStamp = ReadTimestamp(sqsMessage);
                //TODO: We probably need a replyTo header to support request-reply
                receiptHandle = ReadReceiptHandle(sqsMessage);
                
                if (false == (topic.Success && messageId.Success && contentType.Success && correlationId.Success 
                              && handledCount.Success && messageType.Success && timeStamp.Success 
                              && receiptHandle.Success))
                {
                    return FailureMessage(topic, messageId);
                }
                else
                {
                    var messageHeader = timeStamp.Success
                        ? new MessageHeader(messageId.Result, topic.Result, messageType.Result, timeStamp.Result, handledCount.Result, 0)
                        : new MessageHeader(messageId.Result, topic.Result, messageType.Result);

                    message = new Message(messageHeader, new MessageBody(sqsMessage.Body));

                    //we end up putting our existing headers in the bag here
                    //that is fine, as we will tend to look for additional headers by name anyway
                    sqsMessage.MessageAttributes.Each(attribute => message.Header.Bag.Add(attribute.Key, ParseHeaderValue(attribute.Value)));
 
                }

                message.Header.Bag.Add("ReceiptHandle", ((Amazon.SQS.Model.Message)sqsMessage).ReceiptHandle);


            }
            catch (Exception e)
            {
                _logger.Value.WarnException("Failed to create message from amqp message", e);
                message = FailureMessage(topic, messageId);
            }
            
            
            return message;
        }

        private string ParseHeaderValue(MessageAttributeValue attributeValue)
        {
            return attributeValue.StringValue;
        }

        private HeaderResult<DateTime> ReadTimestamp(Amazon.SQS.Model.Message sqsMessage)
        {
            if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.HandledCount, out MessageAttributeValue value))
            {
                if (DateTime.TryParse(value.StringValue, out DateTime timestamp))
                {
                    return new HeaderResult<DateTime>(timestamp, true);
                }
            }
            return new HeaderResult<DateTime>(DateTime.UtcNow, true);
        }

        private HeaderResult<MessageType> ReadMessageType(Amazon.SQS.Model.Message sqsMessage)
        {
            if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.HandledCount, out MessageAttributeValue value))
            {
                if (Enum.TryParse<MessageType>(value.StringValue, out MessageType messageType))
                {
                    return new HeaderResult<MessageType>(messageType, true);
                }
            }
            return new HeaderResult<MessageType>(MessageType.MT_UNACCEPTABLE, false);
        }

        private HeaderResult<int> ReadHandledCount(Amazon.SQS.Model.Message sqsMessage)
        {
            if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.HandledCount, out MessageAttributeValue value))
            {
                if (int.TryParse(value.StringValue, out int handledCount))
                {
                    return new HeaderResult<int>(handledCount, true);
                }
            }
            return new HeaderResult<int>(0, true);
        }

        private HeaderResult<Guid> ReadCorrelationid(Amazon.SQS.Model.Message sqsMessage)
        {
            if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.CorrelationId, out MessageAttributeValue value))
            {
                if (Guid.TryParse(value.StringValue, out Guid correlationId))
                {
                    return new HeaderResult<Guid>(correlationId, true);
                }
            }
            return new HeaderResult<Guid>(Guid.Empty, true);
         }

        private HeaderResult<string> ReadContentType(Amazon.SQS.Model.Message sqsMessage)
        {
            if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.ContentType, out MessageAttributeValue value))
            {
                return new HeaderResult<string>(value.StringValue, true);
            }
            return new HeaderResult<string>(String.Empty, true);
        }

        private HeaderResult<Guid> ReadMessageId(Amazon.SQS.Model.Message sqsMessage)
        {
            if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Id, out MessageAttributeValue value))
            {
                if (Guid.TryParse(value.StringValue, out Guid messageId))
                {
                    return new HeaderResult<Guid>(messageId, true);
                }
            }
            return new HeaderResult<Guid>(Guid.Empty, false);
        }

        private HeaderResult<string> ReadTopic(Amazon.SQS.Model.Message sqsMessage)
        {
            if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Topic, out MessageAttributeValue value))
            {
                return new HeaderResult<string>(value.StringValue, true);
            }
            return new HeaderResult<string>(String.Empty, false);
        }


        private HeaderResult<string> ReadReceiptHandle(Amazon.SQS.Model.Message sqsMessage)
        {
            if (sqsMessage.ReceiptHandle != null)
            {
                return new HeaderResult<string>(sqsMessage.ReceiptHandle, true);
            }
            return new HeaderResult<string>(string.Empty, true);
        }

        private Message FailureMessage(HeaderResult<string> topic, HeaderResult<Guid> messageId)
        {
            var header = new MessageHeader(
                messageId.Success ? messageId.Result : Guid.Empty,
                topic.Success ? topic.Result : string.Empty,
                MessageType.MT_UNACCEPTABLE);
            var message = new Message(header, new MessageBody(string.Empty));
            return message;
        }
    }
}
