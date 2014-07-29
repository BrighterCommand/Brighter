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

using Common.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    public class RMQMessageGateway
    {
        protected ILog Logger;
        protected RMQMessagingGatewayConfigurationSection Configuration;
        protected ConnectionFactory ConnectionFactory;
        protected IConnection Connection;
        protected IModel Channel;
        protected BrokerUnreachableException ConnectionFailure;

        public RMQMessageGateway(ILog logger)
        {
            this.Logger = logger;
            Configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            ConnectionFactory = new ConnectionFactory{Uri = Configuration.AMPQUri.Uri.ToString()};
        }

        protected bool Connect(string queueName)
        {
            try
            {
                if (Connection == null || !Connection.IsOpen)
                {
                    Logger.Debug(m => m("RMQMessagingGateway: Creating connection to Rabbit MQ on AMPQUri {0}", Configuration.AMPQUri.Uri.ToString()));
                    Connection = Connect(ConnectionFactory);

                    Logger.Debug(m => m("RMQMessagingGateway: Opening channel to Rabbit MQ on connection {0}", Configuration.AMPQUri.Uri.ToString()));
                    Channel = OpenChannel(Connection);
                    Channel.BasicQos(0, 1, false);

                    Logger.Debug(m =>m("RMQMessagingGateway: Declaring exchange {0} on connection {1}", Configuration.Exchange.Name, Configuration.AMPQUri.Uri.ToString()));
                    DeclareExchange(Channel, Configuration);

                    Logger.Debug(m =>m("RMQMessagingGateway: Declaring queue {0} on connection {1}", queueName, Configuration.AMPQUri.Uri.ToString()));
                    Channel.QueueDeclare(queueName, false, false, false, null);
                    Channel.QueueBind(queueName, Configuration.Exchange.Name, queueName);

                }
            }
            catch (BrokerUnreachableException e)
            {
                ConnectionFailure = e;
                return false;
            }

            return true;

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

        protected IConnection Connect(ConnectionFactory connectionFactory)
        {
            //create the connection
            var connection = connectionFactory.CreateConnection();
            return connection;
        }

        protected void CloseConnection()
        {
            if (Connection != null)
            {
                if (Connection.IsOpen)
                    Connection.Close();
                Connection.Dispose();
            }

            if (Channel != null)
            {
                Channel.Dispose();
            }
        }
    }
}