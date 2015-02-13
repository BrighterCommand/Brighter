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
using System.Threading.Tasks;
using paramore.brighter.commandprocessor.Logging;
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
    public class MessageGateway : IDisposable
    {
        /// <summary>
        /// The logger
        /// </summary>
        protected readonly ILog Logger;
        /// <summary>
        /// The configuration
        /// </summary>
        protected readonly RMQMessagingGatewayConfigurationSection Configuration;
        /// <summary>
        /// The connection factory
        /// </summary>
        private readonly ConnectionFactory connectionFactory;
        /// <summary>
        /// The connection
        /// </summary>
        protected IConnection Connection;
        /// <summary>
        /// The channel
        /// </summary>
        protected IModel Channel;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public MessageGateway(ILog logger)
        {
            this.Logger = logger;
            Configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            connectionFactory = new ConnectionFactory { Uri = Configuration.AMPQUri.Uri.ToString(), RequestedHeartbeat = 30};
        }

        /// <summary>
        /// Connects the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey"></param>
        /// <param name="createQueues"></param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        protected bool EnsureChannel()
        {
            int retries = Configuration.AMPQUri.ConnectionRetryCount;

            while (true)
            {
                try
                {
                    if (Channel == null || Channel.IsClosed)
                    {
                        if (Channel != null)
                        {
                            try { Channel.Dispose(); }
                            catch (Exception) {}
                            finally { Channel = null; }
                        }

                        GetConnection();

                        Logger.DebugFormat("RMQMessagingGateway: Opening channel to Rabbit MQ on connection {0}", Configuration.AMPQUri.GetSantizedUri());

                        Channel = Connection.CreateModel();

                        // Configure the Quality of service for the model.
                        // BasicQos(0="Don't send me a new message until I’ve finished",  1= "Send me one message at a time", false ="Applied separately to each new consumer on the channel")
                        Channel.BasicQos(0, Configuration.Queues.QosPrefetchSize, false);

                        Logger.DebugFormat("RMQMessagingGateway: Declaring exchange {0} on connection {1}", Configuration.Exchange.Name, Configuration.AMPQUri.GetSantizedUri());
                        DeclareExchange(Channel, Configuration);
                    }

                    break;
                }
                catch (BrokerUnreachableException brokerUnreachableException)
                {
                    if (Configuration.AMPQUri.ConnectionRetryCount == -1)
                    {
                        Logger.ErrorException(
                        "RMQMessagingGateway: Error on connecting to exchange {0} on connection {1}. Will retry {2} times, this is the {3} attempt",
                        brokerUnreachableException,
                        Configuration.Exchange.Name,
                        Configuration.AMPQUri.GetSantizedUri(),
                        Configuration.AMPQUri.ConnectionRetryCount,
                        retries++
                        );

                        Task.Delay(Configuration.AMPQUri.RetryWaitInMilliseconds).Wait();

                        continue;
                    }
                    
                    Logger.WarnException(
                        "RMQMessagingGateway: Error on connecting to exchange {0} on connection {1}. Will retry {2} times, this is the {3} attempt",
                        brokerUnreachableException,
                        Configuration.Exchange.Name,
                        Configuration.AMPQUri.GetSantizedUri(),
                        Configuration.AMPQUri.ConnectionRetryCount,
                        Configuration.AMPQUri.ConnectionRetryCount - retries + 1
                        );

                    if (--retries == 0)
                    {
                        return false;
                    }

                    Task.Delay(Configuration.AMPQUri.RetryWaitInMilliseconds).Wait();
                }
                catch (Exception exception)
                {
                    Logger.WarnException("RMQMessagingGateway: Exception on connecting to exchange {0} on connection {1}",
                                exception,
                                Configuration.Exchange.Name,
                                Configuration.AMPQUri.GetSantizedUri()
                                );
                    throw;
                }
            }

            return true;

        }

        private void GetConnection()
        {
            if (Connection == null || !Connection.IsOpen)
            {
                if (Connection != null)
                {
                    try { Connection.Dispose(); }
                    catch (Exception) {}
                    finally { Connection = null; }
                }

                Logger.DebugFormat("RMQMessagingGateway: Creating connection to Rabbit MQ on AMPQUri {0}", Configuration.AMPQUri.GetSantizedUri());
                Connection = connectionFactory.CreateConnection();
            }
        }

        private void DeclareExchange(IModel channel, RMQMessagingGatewayConfigurationSection configuration)
        {
            //desired state configuration of the exchange
            channel.ExchangeDeclare(configuration.Exchange.Name, ExchangeType.Direct, configuration.Exchange.Durable);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Channel != null)
                {
                    if (Channel.IsOpen) Channel.Close();

                    Channel.Dispose();
                }

                if (Connection != null)
                {
                    if (Connection.IsOpen) Connection.Close();

                    Connection.Dispose();
                }
            }
        }
    }
}