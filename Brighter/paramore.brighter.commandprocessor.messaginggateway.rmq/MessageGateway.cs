// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 07-29-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright ? 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ?Software?), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ?AS IS?, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;
using Polly;
using RabbitMQ.Client;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class RMQMessageGateway.
    /// Base class for messaging gateway used by a <see cref="commandprocessor.Channel"/> to communicate with a RabbitMQ server, to consume messages from the server or
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
        private readonly ConnectionFactory _connectionFactory;
        private readonly ContextualPolicy _retryPolicy;
        private readonly Policy _circuitBreakerPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public MessageGateway()
         : this(LogProvider.GetCurrentClassLogger())
        {}
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway"/> class.
        /// Use if you need to inject a test logger
        /// </summary>
        /// <param name="logger">The logger.</param>
        public MessageGateway(ILog logger) : this(logger, RMQMessagingGatewayConfigurationSection.GetConfiguration())
        {
        }

        protected MessageGateway(string connectionName) 
            : this(LogProvider.GetCurrentClassLogger(), RMQMessagingGatewayConfigurationSection.GetConfiguration(connectionName))
        {
            
        }

        protected MessageGateway(ILog logger, string connectionName) : this(logger, RMQMessagingGatewayConfigurationSection.GetConfiguration(connectionName))
        {
        }
        
        private MessageGateway(ILog logger, RMQMessagingGatewayConfigurationSection configuration)
        {
            Logger = logger;
            Configuration = configuration;

            var connectionPolicyFactory = new ConnectionPolicyFactory(logger, Configuration);

            _retryPolicy = connectionPolicyFactory.RetryPolicy;
            _circuitBreakerPolicy = connectionPolicyFactory.CircuitBreakerPolicy;

            _connectionFactory = new ConnectionFactory { Uri = Configuration.AMPQUri.Uri.ToString(), RequestedHeartbeat = 30 };

            DelaySupported = (this is IAmAMessageGatewaySupportingDelay) && Configuration.Exchange.SupportDelay;

        }

        /// <summary>
        /// The logger
        /// </summary>
        protected readonly ILog Logger;

        /// <summary>
        /// The configuration
        /// </summary>
        protected readonly RMQMessagingGatewayConfigurationSection Configuration;

        /// <summary>
        /// The channel
        /// </summary>
        protected IModel Channel;

        /// <summary>
        /// Gets if the current provider configuration is able to support delayed delivery of messages.
        /// </summary>
        public bool DelaySupported { get; private set; }

        /// <summary>
        /// Connects the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        protected void EnsureChannel(string queueName = "Producer Channel")
        {
            ConnectWithCircuitBreaker(queueName);
        }

        private void ConnectWithCircuitBreaker(string queueName)
        {
            _circuitBreakerPolicy.Execute(() => ConnectWithRetry(queueName));
        }

        private void ConnectWithRetry(string queueName)
        {
            _retryPolicy.Execute(ConnectToBroker, new Dictionary<string, object> { { "queueName", queueName } });
        }

        protected virtual void ConnectToBroker()
        {
            if (Channel == null || Channel.IsClosed)
            {
                var connection = new MessageGatewayConnectionPool().GetConnection(_connectionFactory);

                Logger.DebugFormat("RMQMessagingGateway: Opening channel to Rabbit MQ on connection {0}", Configuration.AMPQUri.GetSanitizedUri());

                Channel = connection.CreateModel();

                //When AutoClose is true, the last channel to close will also cause the connection to close1. If it is set to
                //true before any channel is created, the connection will close then and there.
                if (connection.AutoClose == false)
                {
                    connection.AutoClose = true;
                }

                // Configure the Quality of service for the model.
                // BasicQos(0="Don't send me a new message until I?ve finished",  1= "Send me one message at a time", false ="Applied separately to each new consumer on the channel")
                Channel.BasicQos(0, Configuration.Queues.QosPrefetchSize, false);

                Logger.DebugFormat("RMQMessagingGateway: Declaring exchange {0} on connection {1}", Configuration.Exchange.Name, Configuration.AMPQUri.GetSanitizedUri());

                //desired state configuration of the exchange
                Channel.DeclareExchangeForConfiguration(Configuration);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MessageGateway()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Channel != null)
                {
                    Channel.Abort();
                    Channel = null;
                }
            }
        }
    }
}