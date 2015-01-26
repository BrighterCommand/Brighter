// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 07-29-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
// <copyright file="MessageGateway.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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

using Common.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class RMQMessageGateway.
    /// Base class for messaging gateway used by a <see cref="InputChannel"/> to communicate with a RabbitMQ server, to consume messages from the server or
    /// <see cref="CommandProcessor.Post{T}"/> to send a message to the RabbitMQ server. 
    /// A channel is associated with a queue name, which binds to a <see cref="MessageHeader.Topic"/> when <see cref="CommandProcessor.Post{T}"/> sends over a task queue. 
    /// So to listen for messages on that Topic you need to bind to the matching queue name. 
    /// The configuration holds a &lt;serviceActivatorConnections&gt; section which in turn contains a &lt;connections&gt; collection that contains a set of connections. 
    /// Each connection identifies a mapping between a queue name and a <see cref="IRequest"/> derived type. At runtime we read this list and listen on the associated channels.
    /// The <see cref="MessagePump"/> then uses the <see cref="IAmAMessageMapper"/> associated with the configured request type in <see cref="IAmAMessageMapperRegistry"/> to translate between the 
    /// on-the-wire message and the <see cref="Command"/> or <see cref="Event"/>
    /// </summary>
    public class MessageGateway
    {

        /// <summary>
        /// The logger
        /// </summary>
        protected ILog Logger;
        /// <summary>
        /// The configuration
        /// </summary>
        protected readonly RMQMessagingGatewayConfigurationSection Configuration;
        /// <summary>
        /// The connection factory
        /// </summary>
        readonly ConnectionFactory connectionFactory;
        /// <summary>
        /// The connection
        /// </summary>
        protected IConnection Connection;
        /// <summary>
        /// The channel
        /// </summary>
        protected IModel Channel;
        /// <summary>
        /// The connection failure
        /// </summary>
        protected BrokerUnreachableException ConnectionFailure;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public MessageGateway(ILog logger)
        {
            this.Logger = logger;
            Configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            connectionFactory = new ConnectionFactory{Uri = Configuration.AMPQUri.Uri.ToString()};
        }

        /// <summary>
        /// Connects the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey"></param>
        /// <param name="createQueues"></param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        protected virtual bool Connect(string queueName = "", string routingKey = "", bool createQueues = false)
        {
            try
            {
                if (NotConnected())
                {
                    Logger.Debug(m => m("RMQMessagingGateway: Creating connection to Rabbit MQ on AMPQUri {0}", Configuration.AMPQUri.Uri.ToString()));
                    Connection = Connect(connectionFactory);

                    Logger.Debug(m => m("RMQMessagingGateway: Opening channel to Rabbit MQ on connection {0}", Configuration.AMPQUri.Uri.ToString()));
                    Channel = OpenChannel(Connection);
                    
                    // Configure the Quality of service for the model.
                    // BasicQos(0="Don't send me a new message until I’ve finished",  1= "Send me one message at a time", false ="Applied separately to each new consumer on the channel")
                    Channel.BasicQos(0, Configuration.Queues.QosPrefetchSize, false);

                    Logger.Debug(m => m("RMQMessagingGateway: Declaring exchange {0} on connection {1}", Configuration.Exchange.Name, Configuration.AMPQUri.Uri.ToString()));
                    DeclareExchange(Channel, Configuration);

                    if (createQueues)
                    {
                        Logger.Debug(m => m("RMQMessagingGateway: Creating queue {0} on connection {1}", queueName, Configuration.AMPQUri.Uri.ToString()));
                        
                        Channel.QueueDeclare(queueName, false, false, false, SetQueueArguments());
                        Channel.QueueBind(queueName, Configuration.Exchange.Name, routingKey);
                    }
                }
            }
            catch (BrokerUnreachableException e)
            {
                Logger.Debug(m => m("RMQMessagingGateway: BrokerUnreachableException on connection to queue {0} via exchange {1} on connection {2}", queueName, Configuration.Exchange.Name, Configuration.AMPQUri.Uri.ToString()), e);
                ConnectionFailure = e;
                return false;
            }
            catch (Exception e)
            {
                Logger.Debug(m => m("RMQMessagingGateway: Exception on connection to queue {0} via exchange {1} on connection {2}", queueName, Configuration.Exchange.Name, Configuration.AMPQUri.Uri.ToString()), e);
                throw;
            }

            return true;

        }

        private Dictionary<string, object> SetQueueArguments()
        {
            var arguments = new Dictionary<string, object>();
            QueueIsMirroredAcrossAllNodesInTheCluster(arguments);
            return arguments;
        }

        private void QueueIsMirroredAcrossAllNodesInTheCluster(Dictionary<string, object> arguments)
        {
            if (Configuration.Queues.HighAvailability) { arguments.Add("x-ha-policy", "all"); }
        }

        private void DeclareExchange(IModel channel, RMQMessagingGatewayConfigurationSection configuration)
        {
            //desired state configuration of the exchange
            channel.ExchangeDeclare(configuration.Exchange.Name, ExchangeType.Direct, configuration.Exchange.Durable);
        }

        private IModel OpenChannel(IConnection connection)
        {
            //open a channel on the connection
            var channel = connection.CreateModel();
            return channel;
        }

        /// <summary>
        /// Connects the specified connection factory.
        /// </summary>
        /// <param name="connectionFactory">The connection factory.</param>
        /// <returns>IConnection.</returns>
        protected IConnection Connect(ConnectionFactory connectionFactory)
        {
            //create the connection
            var connection = connectionFactory.CreateConnection();
            return connection;
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
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

        protected bool NotConnected()
        {
            return Connection == null || !Connection.IsOpen;
        }
    }
}