using System;
using System.Collections.Generic;
using System.Text;

using paramore.brighter.commandprocessor.extensions;

using RabbitMQ.Client;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    public class RmqMessagePublisher
    {
        private readonly IModel _channel;
        private readonly string _exchangeName;

        public RmqMessagePublisher(IModel channel, string exchangeName)
        {
            if (channel == null)
            {
                throw new ArgumentNullException("channel");
            }
            if (exchangeName == null)
            {
                throw new ArgumentNullException("exchangeName");
            }

            _channel = channel;
            _exchangeName = exchangeName;
        }

        public void PublishMessage(Message message)
        {
            _channel.BasicPublish(
                _exchangeName,
                message.Header.Topic,
                false,
                false,
                CreateBasicProperties(message),
                Encoding.UTF8.GetBytes(message.Body.Value));
        }

        private IBasicProperties CreateBasicProperties(Message message)
        {
            var basicProperties = _channel.CreateBasicProperties();
            basicProperties.DeliveryMode = 1;
            basicProperties.ContentType = "text/plain";
            basicProperties.MessageId = message.Id.ToString();
            basicProperties.Timestamp = new AmqpTimestamp(UnixTimestamp.GetCurrentUnixTimestampSeconds());
            basicProperties.Headers = new Dictionary<string, object>
                                      {
                                          {"MessageType", message.Header.MessageType.ToString()},
                                          {"Topic", message.Header.Topic}
                                      };

            message.Header.Bag.Each((header) => basicProperties.Headers.Add(new KeyValuePair<string, object>(header.Key, header.Value)));
            return basicProperties;
        }

        
    }
}