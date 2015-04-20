// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : AnthonyP
// Created          : 08-04-2015
//
// Last Modified By : 
// Last Modified On : 
// ***********************************************************************
// <copyright file="MessageGatewayConnectionPool.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using paramore.brighter.commandprocessor.Logging;
using RabbitMQ.Client;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class MessageGatewayConnectionPool.
    /// </summary>
    public class MessageGatewayConnectionPool
    {
        private static readonly Dictionary<string, IConnection> s_connectionPool = new Dictionary<string, IConnection>();
        private static readonly object s_lock = new object();
        private static readonly ILog s_logger = LogProvider.GetCurrentClassLogger();

        /// <summary>
        /// Return matching RabbitMQ connection if exist (match by amqp scheme)
        /// or create new connection to RabbitMQ (thread-safe)
        /// </summary>
        /// <param name="connectionFactory"></param>
        /// <returns></returns>
        public IConnection GetConnection(ConnectionFactory connectionFactory)
        {
            var connectionId = GetConnectionId(connectionFactory);

            IConnection connection;
            var connectionFound = s_connectionPool.TryGetValue(connectionId, out connection);

            if (connectionFound != false && connection.IsOpen != false) 
                return connection;

            lock (s_lock)
            {
                connectionFound = s_connectionPool.TryGetValue(connectionId, out connection);

                if (connectionFound == false || connection.IsOpen == false)
                {
                    connection = CreateConnection(connectionFactory);
                }
            }

            return connection;
        }

        private IConnection CreateConnection(ConnectionFactory connectionFactory)
        {
            var connectionId = GetConnectionId(connectionFactory);

            TryRemoveConnection(connectionId);

            s_logger.DebugFormat("RMQMessagingGateway: Creating connection to Rabbit MQ endpoint {0}", connectionFactory.Endpoint);

            var connection = connectionFactory.CreateConnection();

            connection.ConnectionShutdown += delegate { TryRemoveConnection(connectionId); };

            s_connectionPool.Add(connectionId, connection);
            
            return connection;
        }

        private void TryRemoveConnection(string connectionId)
        {
            if(s_connectionPool.ContainsKey(connectionId))
                s_connectionPool.Remove(connectionId);
        }

        private string GetConnectionId(ConnectionFactory connectionFactory)
        {
            return string.Concat(connectionFactory.UserName, ".", connectionFactory.Password, ".", connectionFactory.HostName, ".", connectionFactory.Port, ".", connectionFactory.VirtualHost).ToLowerInvariant();
        }
    }
}