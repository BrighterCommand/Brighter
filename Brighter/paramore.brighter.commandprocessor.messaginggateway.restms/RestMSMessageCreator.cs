using System;
using System.Linq;
using System.Text;
using paramore.brighter.commandprocessor.extensions;
using paramore.brighter.commandprocessor.messaginggateway.restms.Model;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    public static class RestMSMessageCreator
    {
        public static Message CreateMessage(RestMSMessage restMSMessage)
        {
            var header = ReadMessageHeaders(restMSMessage);
            var body = ReadMessageBody(restMSMessage);

            return new Message(header, body);
        }

        static MessageBody ReadMessageBody(RestMSMessage restMSMessage)
        {
            var encoding = Encoding.GetEncoding(restMSMessage.Content.Encoding);
            return new MessageBody(encoding.GetString(GetBytes(restMSMessage.Content.Value)));
        }

        static MessageHeader ReadMessageHeaders(RestMSMessage restMSMessage)
        {
            bool failure = false;

            var messageId = Guid.Empty;
            if (!Guid.TryParse(restMSMessage.MessageId, out messageId))
                return FailureMessageHeader(messageId, restMSMessage.Address);

            var messageType = MessageType.MT_NONE;
            if (!restMSMessage.Headers.Any())
                return FailureMessageHeader(messageId, restMSMessage.Address);

            var messageTypeString = restMSMessage.Headers.First(header => header.Name == "MessageType").Value;
            if (messageTypeString == null)
                return FailureMessageHeader(messageId, restMSMessage.Address);

            if (!Enum.TryParse<MessageType>(messageTypeString, out messageType))
                return FailureMessageHeader(messageId, restMSMessage.Address);

            var messageHeader = new MessageHeader(messageId, restMSMessage.Address, messageType);
            restMSMessage.Headers.Each(header => messageHeader.Bag.Add(header.Name, header.Value));
            return messageHeader;
        }

        static MessageHeader FailureMessageHeader(Guid messageId, string topic)
        {
            return new MessageHeader(messageId, topic, MessageType.MT_UNACCEPTABLE);
        }

        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
