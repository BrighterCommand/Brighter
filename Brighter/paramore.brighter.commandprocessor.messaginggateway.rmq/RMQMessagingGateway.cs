using System;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.Client.Events;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    public class RMQMessagingGateway : IAmAMessagingGateway 
    {
        private readonly ILog logger;
        private readonly RMQMessagingGatewayConfigurationSection configuration;
        private readonly ConnectionFactory connectionFactory;
        private IConnection connection;
        private IModel channel;
        private BrokerUnreachableException connectionFailure;
        const bool autoAck = false;

        public RMQMessagingGateway(ILog logger)
        {
            this.logger = logger;
            configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            connectionFactory = new ConnectionFactory{Uri = configuration.AMPQUri.Uri.ToString()};
        }

        public void AcknowledgeMessage(Message message)
         {
            if (channel != null)
            {
                logger.Debug(m => m("Acknowledging message {0} as completed", message.Id));
                channel.BasicAck((ulong)message.Header.Bag["DeliveryTag"], false);
            }
         }

        public Message Listen(string queueName, int timeoutInMilliseconds)
        {

            logger.Debug(m => m("Preparing  to retrieve next message via exchange {1}", configuration.Exchange.Name));

            if (!Connect())
            {
                throw connectionFailure;
            }

            //create an 'empty' message
            var message = CreateEmptyMessage();
            try
            {
                var consumer = new QueueingBasicConsumer(channel);
                channel.BasicConsume(queueName, autoAck, consumer);
                BasicDeliverEventArgs fromQueue;
                consumer.Queue.Dequeue(timeoutInMilliseconds, out fromQueue);
                if (fromQueue != null)
                {
                    var messageId = fromQueue.BasicProperties.MessageId;
                    message = new Message(
                        new MessageHeader(Guid.Parse(messageId), fromQueue.RoutingKey, MessageType.MT_EVENT),
                        new MessageBody(Encoding.UTF8.GetString(fromQueue.Body))
                        );

                    message.Header.Bag["DeliveryTag"] = fromQueue.DeliveryTag;
                }
            }
            catch (Exception e)
            {
                logger.Error(m => m("There was an error listening to channel {0} of {1}", queueName, e.ToString()));
                throw;
            }

            return message;

        }

        public Task SendMessage(Message message)
        {
            //RabbitMQ .NET Client does not have an async publish, so fake this for now as we want to support messaging frameworks that do have this option
            var tcs = new TaskCompletionSource<object>();

            logger.Debug(m=> m("Preparing  to sending message {0} via exchange {1}", configuration.Exchange.Name, JsonConvert.SerializeObject(message)));

            if (!Connect())
            {
                tcs.SetException(connectionFailure);
                throw connectionFailure;
            }

            try
            {
                logger.Debug(m => m("Publishing message to exchange {0} on connection {1} with topic {2} and id {3}", configuration.Exchange.Name, configuration.AMPQUri.Uri.ToString(), message.Header.Topic, message.Id));
                PublishMessage(message, channel, configuration, CreateMessageHeader(message, channel));
                logger.Debug(m => m("Published message to exchange {0} on connection {1} with topic {2} and id {3} at {4}", configuration.Exchange.Name, configuration.AMPQUri.Uri.ToString(), message.Header.Topic, message.Id, DateTime.UtcNow));
            }
            catch (Exception e)
            {
                if (channel != null)
                    channel.TxRollback();
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

        private bool Connect()
        {
            try
            {
                if (connection == null || !connection.IsOpen)
                {
                    logger.Debug(m => m("Creating connection to Rabbit MQ on AMPQUri {0}", configuration.AMPQUri.Uri.ToString()));
                    connection = Connect(connectionFactory);

                    logger.Debug(m => m("Opening channel to Rabbit MQ on connection {0}", configuration.AMPQUri.Uri.ToString()));
                    channel = OpenChannel(connection);
                    channel.BasicQos(0, 1, false);

                    logger.Debug(m =>m("Declaring exchange {0} on connection {1}", configuration.Exchange.Name,configuration.AMPQUri.Uri.ToString()));
                    DeclareExchange(channel, configuration);
                }
            }
            catch (BrokerUnreachableException e)
            {
                connectionFailure = e;
                return false;
            }

            return true;

        }

        private static Message CreateEmptyMessage()
        {
            var message = new Message(new MessageHeader(Guid.Empty, string.Empty, MessageType.MT_NONE),
                                      new MessageBody(string.Empty));
            return message;
        }

        private IBasicProperties CreateMessageHeader(Message message, IModel channel)
        {
            //create message header
            var basicProperties = channel.CreateBasicProperties();
            basicProperties.DeliveryMode = 1;
            basicProperties.ContentType = "text/plain";
            basicProperties.MessageId = message.Id.ToString();
            return basicProperties;
        }

        private void DeclareExchange(IModel channel, RMQMessagingGatewayConfigurationSection configuration)
        {
            //desired state configuration of the exchange
            channel.ExchangeDeclare(configuration.Exchange.Name, ExchangeType.Direct, false);
        }

        private IModel OpenChannel(IConnection connection)
        {
            //open a channel on the connection
            var channel = connection.CreateModel();
            return channel;
        }

        private IConnection Connect(ConnectionFactory connectionFactory)
        {
            //create the connection
            var connection = connectionFactory.CreateConnection();
            return connection;
        }

        private void PublishMessage(Message message, IModel channel, RMQMessagingGatewayConfigurationSection configuration, IBasicProperties basicProperties)
        {
            //publish message
            channel.TxSelect();
            channel.BasicPublish(configuration.Exchange.Name, message.Header.Topic, false, false, basicProperties,
                                 Encoding.UTF8.GetBytes(message.Body.Value));
            channel.TxCommit();
        }
    }
}
