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

using System;
using System.Collections.Generic;
using Paramore.Brighter.MessagingGateway.RMQ.Logging;
using RabbitMQ.Client;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Class MessageGatewayConnectionPool.
    /// </summary>
    public class RMQMessageGatewayConnectionPool
    {
        private static readonly Dictionary<string, IConnection> s_connectionPool = new Dictionary<string, IConnection>();
        private static readonly object s_lock = new object();
        private static readonly Lazy<ILog> s_logger = new Lazy<ILog>(LogProvider.For<RMQMessageGatewayConnectionPool>);

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

        public void ResetConnection(ConnectionFactory connectionFactory)
        {
            var connectionId = GetConnectionId(connectionFactory);
            
            lock (s_lock)
            {
                var connection = s_connectionPool[connectionId];
                connection?.Dispose();
                s_connectionPool.Remove(connectionId);
                CreateConnection(connectionFactory);
            }
       }

        private IConnection CreateConnection(ConnectionFactory connectionFactory)
        {
            var connectionId = GetConnectionId(connectionFactory);

            TryRemoveConnection(connectionId);

            s_logger.Value.DebugFormat("RMQMessagingGateway: Creating connection to Rabbit MQ endpoint {0}", connectionFactory.Endpoint);

            connectionFactory.RequestedHeartbeat = 5;
            connectionFactory.RequestedConnectionTimeout = 5000;
            connectionFactory.SocketReadTimeout = 5000;
            connectionFactory.SocketWriteTimeout = 5000;
            
            var connection = connectionFactory.CreateConnection();
            
            s_logger.Value.DebugFormat("RMQMessagingGateway: new connected to {0} added to pool named {1}", connection.Endpoint, connection.ClientProvidedName);

            connection.ConnectionShutdown += delegate { TryRemoveConnection(connectionId); };

            s_connectionPool.Add(connectionId, connection);
            
            return connection;
        }

        private void TryRemoveConnection(string connectionId)
        {
            if (s_connectionPool.ContainsKey(connectionId))
            {
                var connection = s_connectionPool[connectionId];
                if (connection != null)
                {
                    if (connection.IsOpen)
                        connection.Close();
                    connection.Dispose();
                }

                s_connectionPool.Remove(connectionId);
            }
        }

        private string GetConnectionId(ConnectionFactory connectionFactory)
        {
            return string.Concat(connectionFactory.UserName, ".", connectionFactory.Password, ".", connectionFactory.HostName, ".", connectionFactory.Port, ".", connectionFactory.VirtualHost).ToLowerInvariant();
        }
    }
}