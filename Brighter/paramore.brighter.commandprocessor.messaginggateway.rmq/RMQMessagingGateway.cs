using System;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    public class RMQMessagingGateway : IAmAMessagingGateway
    {
        private readonly ILog logger;

        public RMQMessagingGateway(ILog logger)
        {
            this.logger = logger;
        }

        public Task SendMessage(Message message)
        {
            //RabbitMQ .NET Client does not have an async publish, so fake this for now as we want to support messaging frameworks that do have this option
            var tcs = new TaskCompletionSource<object>();

            var configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            var connectionFactory = new ConnectionFactory{Uri = configuration.AMPQUri.Uri.ToString()};

            logger.Debug(m=> m("Preparing  to sending message {0} via exchange {1}", configuration.Exchange.Name, JsonConvert.SerializeObject(message)));

            IConnection connection = null;
            IModel channel = null;
            try
            {
                logger.Debug(m=> m("Creating connection to Rabbit MQ on AMPQUri {0}", configuration.AMPQUri.Uri.ToString()));
                connection = Connect(connectionFactory);

                logger.Debug(m=> m("Opening channel to Rabbit MQ on connection {0}", configuration.AMPQUri.Uri.ToString()));
                channel = OpenChannel(connection);

                logger.Debug(m => m("Declaring exchange {0} on connection {1}", configuration.Exchange.Name, configuration.AMPQUri.Uri.ToString()));
                DeclareExchange(channel, configuration);

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
            finally
            {
                if (connection != null) connection.Dispose();
                if (channel != null) channel.Dispose();
            }

            tcs.SetResult(new object());
            return tcs.Task;
        }

        private static void PublishMessage(Message message, IModel channel,
                                           RMQMessagingGatewayConfigurationSection configuration,
                                           IBasicProperties basicProperties)
        {
            //publish message
            channel.TxSelect();
            channel.BasicPublish(configuration.Exchange.Name, message.Header.Topic, false, false, basicProperties,
                                 Encoding.UTF8.GetBytes(message.Body.Value));
            channel.TxCommit();
        }

        private static IBasicProperties CreateMessageHeader(Message message, IModel channel)
        {
            //create message header
            var basicProperties = channel.CreateBasicProperties();
            basicProperties.DeliveryMode = 1;
            basicProperties.ContentType = "text/plain";
            basicProperties.MessageId = message.Id.ToString();
            return basicProperties;
        }

        private static void DeclareExchange(IModel channel, RMQMessagingGatewayConfigurationSection configuration)
        {
            //desired state configuration of the exchange
            channel.ExchangeDeclare(configuration.Exchange.Name, ExchangeType.Direct, false);
        }

        private static IModel OpenChannel(IConnection connection)
        {
            //open a channel on the connection
            var channel = connection.CreateModel();
            return channel;
        }

        private static IConnection Connect(ConnectionFactory connectionFactory)
        {
            //create the connection
            var connection = connectionFactory.CreateConnection();
            return connection;
        }
    }
}
