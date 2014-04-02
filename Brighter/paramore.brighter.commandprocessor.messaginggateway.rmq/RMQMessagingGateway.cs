using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
     //TODO: Add some logging - important to log at a gateway
    public class RMQMessagingGateway : IAmAMessagingGateway
    {
        public Task SendMessage(Message message)
        {
            //RabbitMQ .NET Client does not have an async publish, so fake this for now as we want to support messaging frameworks that do have this option
            var tcs = new TaskCompletionSource<object>();

            var configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            var connectionFactory = new ConnectionFactory{Uri = configuration.AMPQUri.Uri.ToString()};

            IConnection connection = null;
            IModel channel = null;
            try
            {
                connection = Connect(connectionFactory);

                channel = OpenChannel(connection);

                DeclareExchange(channel, configuration);

                PublishMessage(message, channel, configuration, CreateMessageHeader(message, channel));
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
