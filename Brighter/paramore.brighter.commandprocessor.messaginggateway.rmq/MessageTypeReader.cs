using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Logging;
using RabbitMQ.Client.Events;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    public class MessageTypeReader
    {
        readonly ILog _logger;

        public MessageTypeReader(ILog logger)
        {
            _logger = logger;
        }

        public MessageType GetMessageType(BasicDeliverEventArgs fromQueue)
        {
            IDictionary<string, object> headers = fromQueue.BasicProperties.Headers;
            if (headers.ContainsKey(HeaderNames.MESSAGE_TYPE))
            {
                var mtBytes = headers[HeaderNames.MESSAGE_TYPE] as byte[];
                if (null == mtBytes)
                {
                    _logger.Error("Failed to read message type as byte[] - header type is " + headers[HeaderNames.MESSAGE_TYPE].GetType());
                    return MessageType.MT_UNACCEPTABLE;
                }

                try
                {
                    MessageType type;
                    var str = Encoding.UTF8.GetString(mtBytes);
                    if(Enum.TryParse(str, true, out type))
                        return type;
                    return MessageType.MT_UNACCEPTABLE;
                }
                catch (ArgumentException e)
                {
                    var firstTwentyBytes = BitConverter.ToString(mtBytes.Take(20).ToArray());
                    _logger.Error("Failed to read message type bytes as string. First 20 bytes follow. \n "+firstTwentyBytes, e);
                    return MessageType.MT_UNACCEPTABLE;
                }
            }
            _logger.Debug("Falling back to MessageType event.");
            return MessageType.MT_EVENT;
        }
    }
}