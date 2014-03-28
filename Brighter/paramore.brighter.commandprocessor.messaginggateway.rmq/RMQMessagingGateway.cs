using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace paramore.brighter.commandprocessor.messaginggateway._0mq
{
    //TODO: Add some logging - important to log at a gateway
    public class RMQMessagingGateway : IAmAMessagingGateway
    {
        public Task SendMessage(Message message)
        {
            var connectionFactory = new ConnectionFactory();
            //TODO: Configure connection from config file: host, port, username, password, default protocol, heartbeat in seconds

            IConnection connection = null;
            IModel model = null;
            try
            {
                connection = connectionFactory.CreateConnection();
                model = connection.CreateModel();

                var basicProperties = model.CreateBasicProperties();
                basicProperties.DeliveryMode = 1; 
                //TODO: do we want JSONify the object graph of the message?
                basicProperties.ContentType = "application/json";
                basicProperties.MessageId = message.Id.ToString();

                model.TxSelect();
                //TODO: configure exchange name in config file
                model.BasicPublish(exchange, message.Header.Topic, false, false, basicProperties, Encoding.UTF8.GetBytes(message.Body.Value));
                model.TxCommit();
            }
            catch (Exception)
            {
                if (model != null)
                    model.TxRollback();
            }
            finally
            {
                if (connection != null) connection.Dispose();
                if (model != null) model.Dispose();
            }
        }
    }
}
