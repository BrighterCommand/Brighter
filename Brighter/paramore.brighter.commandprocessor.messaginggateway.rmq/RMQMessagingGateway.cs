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
            //TODO: We want to make this async if we can
            var tcs = new TaskCompletionSource<object>();

            var configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration(); 
            var connectionFactory = new ConnectionFactory{Uri = configuration.AMPQUri.Uri.ToString()};

            IConnection connection = null;
            IModel channel = null;
            try
            {
                //create the connection
                connection = connectionFactory.CreateConnection();
                //open a channel on the connection
                channel = connection.CreateModel();
                //desired state configuration of the exchange
                channel.ExchangeDeclare(configuration.Exchange.Name, ExchangeType.Direct, false);

                //create message header
                var basicProperties = channel.CreateBasicProperties();
                basicProperties.DeliveryMode = 1; 
                //TODO: do we want JSONify the object graph of the message?
                basicProperties.ContentType = "application/json";
                basicProperties.MessageId = message.Id.ToString();

                //publish message
                channel.TxSelect();
                channel.BasicPublish(configuration.Exchange.Name, message.Header.Topic, false, false, basicProperties, Encoding.UTF8.GetBytes(message.Body.Value));
                channel.TxCommit();
            }
            catch (Exception)
            {
                if (channel != null)
                    channel.TxRollback();
            }
            finally
            {
                if (connection != null) connection.Dispose();
                if (channel != null) channel.Dispose();
            }

            tcs.SetResult(new object());
            return tcs.Task;
        }
    }
}
