#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.extensions;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    public class RMQMessagingGateway : IAmAMessagingGateway
    {
        private const bool autoAck = false;
        private readonly RmqMessageCreator _messageCreator;
        private readonly RMQMessagingGatewayConfigurationSection configuration;
        private readonly ConnectionFactory connectionFactory;
        private readonly ILog logger;
        private IModel channel;
        private IConnection connection;
        private QueueingBasicConsumer consumer;
        private BrokerUnreachableException connectionFailure;
        private QueueingBasicConsumer consumer;

        public RMQMessagingGateway(ILog logger)
        {
            this.logger = logger;
            configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            connectionFactory = new ConnectionFactory {Uri = configuration.AMPQUri.Uri.ToString()};
            _messageCreator = new RmqMessageCreator(logger);
        }

        public RmqMessageCreator MessageCreator
        {
            get { return _messageCreator; }
        }

        public void Acknowledge(Message message)
        {
            if (channel != null)
            {
                var deliveryTag = (ulong) message.Header.Bag["DeliveryTag"];
                logger.Debug(
                    m =>
                        m("RMQMessagingGateway: Acknowledging message {0} as completed with delivery tag {1}",
                            message.Id, deliveryTag));
                channel.BasicAck(deliveryTag, false);
            }
        }

        public void Purge(string queueName)
        {
            if (channel != null)
            {
                logger.Debug(m => m("RMQMessagingGateway: Purging channel"));
                channel.QueuePurge(queueName);
            }
        }

        public void Reject(Message message, bool requeue)
        {
            if (channel != null)
            {
                logger.Debug(m => m("RMQMessagingGateway: NoAck message {0}", message.Id));
                channel.BasicNack((ulong) message.Header.Bag["DeliveryTag"], false, requeue);
            }
        }


        public Message Receive(string queueName, int timeoutInMilliseconds)
        {
            logger.Debug(
                m =>
                    m("RMQMessagingGateway: Preparing  to retrieve next message via exchange {0}",
                        configuration.Exchange.Name));

            if (!Connect(queueName))
            {
                logger.Debug(
                    m => m("RMQMessagingGateway: Unable to connect to the exchange {0}", configuration.Exchange.Name));
                throw connectionFailure;
            }

            var message = new Message();
            try
            {
                BasicDeliverEventArgs fromQueue;
                consumer.Queue.Dequeue(timeoutInMilliseconds, out fromQueue);
                if (fromQueue != null)
                {
                    message = MessageCreator.CreateMessage(fromQueue);
                    logger.Debug(
                        m =>
                            m(
                                "RMQMessagingGateway: Recieved message with delivery tag {5} from exchange {0} on connection {1} with topic {2} and id {3} and body {4}",
                                configuration.Exchange.Name, configuration.AMPQUri.Uri.ToString(), message.Header.Topic,
                                message.Id, message.Body.Value, message.Header.Bag["DeliveryTag"]));
                }
                else
                {
                    logger.Debug(
                        m =>
                            m(
                                "RMQMessagingGateway: Time out without recieving message from exchange {0} on connection {1} with topic {2}",
                                configuration.Exchange.Name, configuration.AMPQUri.Uri.ToString(), queueName));
                }
            }
            catch (Exception e)
            {
                logger.Error(
                    m =>
                        m("RMQMessagingGateway: There was an error listening to channel {0} of {1}", queueName,
                            e.ToString()));
                throw;
            }

            return message;
        }


        public Task Send(Message message)
        {
            //RabbitMQ .NET Client does not have an async publish, so fake this for now as we want to support messaging frameworks that do have this option
            var tcs = new TaskCompletionSource<object>();

            logger.Debug(
                m =>
                    m("RMQMessagingGateway: Preparing  to sending message {0} via exchange {1}",
                        configuration.Exchange.Name, JsonConvert.SerializeObject(message)));

            if (!Connect(message.Header.Topic))
            {
                tcs.SetException(connectionFailure);
                throw connectionFailure;
            }

            try
            {
                logger.Debug(
                    m =>
                        m(
                            "RMQMessagingGateway: Publishing message to exchange {0} on connection {1} with topic {2} and id {3} and body: {4}",
                            configuration.Exchange.Name, configuration.AMPQUri.Uri.ToString(), message.Header.Topic,
                            message.Id, message.Body.Value));
                PublishMessage(message, channel, configuration, CreateMessageHeader(message, channel));
                logger.Debug(
                    m =>
                        m(
                            "RMQMessagingGateway: Published message to exchange {0} on connection {1} with topic {2} and id {3} and body: {4} at {5}",
                            configuration.Exchange.Name, configuration.AMPQUri.Uri.ToString(), message.Header.Topic,
                            message.Id, message.Body.Value, DateTime.UtcNow));
            }
            catch (Exception e)
            {
                if (channel != null)
                    tcs.SetException(e);
                throw;
            }

            tcs.SetResult(new object());
            return tcs.Task;
        }

        public void Dispose()
        {
            if (connection != null)
            {
                if (connection.IsOpen)
                    connection.Close();
                connection.Dispose();
            }

            if (channel != null)
            {
                channel.Dispose();
            }
        }

        private bool Connect(string queueName)
        {
            try
            {
                if (connection == null || !connection.IsOpen)
                {
                    logger.Debug(
                        m =>
                            m("RMQMessagingGateway: Creating connection to Rabbit MQ on AMPQUri {0}",
                                configuration.AMPQUri.Uri.ToString()));
                    connection = Connect(connectionFactory);

                    logger.Debug(
                        m =>
                            m("RMQMessagingGateway: Opening channel to Rabbit MQ on connection {0}",
                                configuration.AMPQUri.Uri.ToString()));
                    channel = OpenChannel(connection);
                    channel.BasicQos(0, 1, false);

                    logger.Debug(
                        m =>
                            m("RMQMessagingGateway: Declaring exchange {0} on connection {1}",
                                configuration.Exchange.Name, configuration.AMPQUri.Uri.ToString()));
                    DeclareExchange(channel, configuration);

                    logger.Debug(
                        m =>
                            m("RMQMessagingGateway: Declaring queue {0} on connection {1}", queueName,
                                configuration.AMPQUri.Uri.ToString()));
                    channel.QueueDeclare(queueName, false, false, false, null);
                    channel.QueueBind(queueName, configuration.Exchange.Name, queueName);
                    consumer = new QueueingBasicConsumer(channel);
                    channel.BasicConsume(queueName, autoAck, consumer);
                    consumer = new QueueingBasicConsumer(channel);
                    channel.BasicConsume(queueName, autoAck, consumer);

                }
            }
            catch (BrokerUnreachableException e)
            {
                connectionFailure = e;
                logger.Error("Failed to connect to broker", e);
                return false;
            }

            return true;
        }


        private IBasicProperties CreateMessageHeader(Message message, IModel channel)
        {
            //create message header
            IBasicProperties basicProperties = channel.CreateBasicProperties();
            basicProperties.DeliveryMode = 1;
            basicProperties.ContentType = "text/plain";
            basicProperties.MessageId = message.Id.ToString();
            basicProperties.Headers = new Dictionary<string, object>
            {
                {HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString()},
                {HeaderNames.TOPIC, message.Header.Topic}
            };
            message.Header.Bag.Each(
                header => basicProperties.Headers.Add(new KeyValuePair<string, object>(header.Key, header.Value)));
            return basicProperties;
        }

        private void DeclareExchange(IModel channel, RMQMessagingGatewayConfigurationSection configuration)
        {
            Exchange exchange = configuration.Exchange;
            channel.ExchangeDeclare(exchange.Name, exchange.Type, exchange.Durable);
        }

        private IModel OpenChannel(IConnection connection)
        {
            //open a channel on the connection
            IModel channel = connection.CreateModel();
            return channel;
        }

        private IConnection Connect(ConnectionFactory connectionFactory)
        {
            //create the connection
            IConnection connection = connectionFactory.CreateConnection();
            return connection;
        }

        private void PublishMessage(Message message, IModel channel,
            RMQMessagingGatewayConfigurationSection configuration,
            IBasicProperties basicProperties)
        {
            //publish message
            channel.BasicPublish(configuration.Exchange.Name, message.Header.Topic, false, false, basicProperties,
                Encoding.UTF8.GetBytes(message.Body.Value));
        }
    }
}