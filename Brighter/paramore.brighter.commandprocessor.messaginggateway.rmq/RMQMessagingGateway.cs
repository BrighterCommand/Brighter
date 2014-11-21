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
using System.Linq;
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
        const bool autoAck = false;
        readonly RMQMessagingGatewayConfigurationSection configuration;
        readonly ConnectionFactory connectionFactory;
        readonly ILog logger;
        IModel channel;
        IConnection connection;
        BrokerUnreachableException connectionFailure;
        readonly MessageTypeReader _messageTypeReader;

        public RMQMessagingGateway(ILog logger)
        {
            this.logger = logger;
            configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            connectionFactory = new ConnectionFactory {Uri = configuration.AMPQUri.Uri.ToString()};
            _messageTypeReader = new MessageTypeReader(logger);
        }

        public void Acknowledge(Message message)
        {
            if (channel != null)
            {
                logger.Debug(m => m("RMQMessagingGateway: Acknowledging message {0} as completed", message.Id));
                channel.BasicAck((ulong) message.Header.Bag["DeliveryTag"], false);
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
                var consumer = new QueueingBasicConsumer(channel);
                channel.BasicConsume(queueName, autoAck, consumer);
                BasicDeliverEventArgs fromQueue;
                consumer.Queue.Dequeue(timeoutInMilliseconds, out fromQueue);
                if (fromQueue != null)
                {
                    message = CreateMessage(fromQueue);
                    logger.Debug(
                        m =>
                            m(
                                "RMQMessagingGateway: Recieved message from exchange {0} on connection {1} with topic {2} and id {3} and body {4}",
                                configuration.Exchange.Name, configuration.AMPQUri.Uri.ToString(), message.Header.Topic,
                                message.Id, message.Body.Value));
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

        bool Connect(string queueName)
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

        Message CreateMessage(BasicDeliverEventArgs fromQueue)
        {
            var headers = fromQueue.BasicProperties.Headers;
            try
            {
                var topic = ReadTopic(fromQueue, headers);
                var messageId = ReadMessageId(headers);

                string body = Encoding.UTF8.GetString(fromQueue.Body);

                var message = new Message(
                    new MessageHeader(messageId, topic, _messageTypeReader.GetMessageType(fromQueue)),
                    new MessageBody(body));

                headers.Each(
                    header => message.Header.Bag.Add(header.Key, Encoding.UTF8.GetString((byte[]) header.Value)));

                message.Header.Bag["DeliveryTag"] = fromQueue.DeliveryTag;
                return message;
            }
            catch (Exception e)
            {
                var sb = new StringBuilder("Failed to create message from amqp message\n");
                headers.Each(
                    header =>
                        sb.AppendFormat("\t* {0}: {1}\n", header.Key, Encoding.UTF8.GetString((byte[]) header.Value)));

                logger.Warn(sb.ToString(), e);
                throw;
            }
        }

        string ReadTopic(BasicDeliverEventArgs fromQueue, IDictionary<string, object> headers)
        {
            string topic = fromQueue.RoutingKey;
            if (headers.ContainsKey("Topic"))
                topic = Encoding.UTF8.GetString((byte[]) headers["Topic"]);
            else
                logger.Debug("No topic found in message headers, defaulting to routing key " + fromQueue.RoutingKey);
            return topic;
        }

        Guid ReadMessageId(IDictionary<string, object> headers)
        {
            Guid messageId = Guid.NewGuid();
            if (headers.ContainsKey("MessageId"))
                Guid.TryParse(headers["MessageId"] as string, out messageId);
            else
                logger.Debug("No message id found in message, new message id is " + messageId);
            return messageId;
        }


        IBasicProperties CreateMessageHeader(Message message, IModel channel)
        {
            //create message header
            IBasicProperties basicProperties = channel.CreateBasicProperties();
            basicProperties.DeliveryMode = 1;
            basicProperties.ContentType = "text/plain";
            basicProperties.MessageId = message.Id.ToString();
            basicProperties.Headers = new Dictionary<string, object>
            {
                {HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString()},
                {"Topic", message.Header.Topic}
            };
            message.Header.Bag.Each(
                header => basicProperties.Headers.Add(new KeyValuePair<string, object>(header.Key, header.Value)));
            return basicProperties;
        }

        void DeclareExchange(IModel channel, RMQMessagingGatewayConfigurationSection configuration)
        {
            Exchange exchange = configuration.Exchange;
            channel.ExchangeDeclare(exchange.Name, exchange.Type, exchange.Durable);
        }

        IModel OpenChannel(IConnection connection)
        {
            //open a channel on the connection
            IModel channel = connection.CreateModel();
            return channel;
        }

        IConnection Connect(ConnectionFactory connectionFactory)
        {
            //create the connection
            IConnection connection = connectionFactory.CreateConnection();
            return connection;
        }

        void PublishMessage(Message message, IModel channel, RMQMessagingGatewayConfigurationSection configuration,
            IBasicProperties basicProperties)
        {
            //publish message
            channel.BasicPublish(configuration.Exchange.Name, message.Header.Topic, false, false, basicProperties,
                Encoding.UTF8.GetBytes(message.Body.Value));
        }
    }
}