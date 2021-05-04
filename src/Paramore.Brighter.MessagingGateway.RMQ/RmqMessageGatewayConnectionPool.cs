﻿#region Licence
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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Class MessageGatewayConnectionPool.
    /// </summary>
    public class RmqMessageGatewayConnectionPool
    {
        private readonly string _connectionName;
        private readonly ushort _connectionHeartbeat;
        private static readonly Dictionary<string, PooledConnection> s_connectionPool = new Dictionary<string, PooledConnection>();
        private static readonly object s_lock = new object();
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageGatewayConnectionPool>();
        private static readonly Random jitter = new Random();

        public RmqMessageGatewayConnectionPool(string connectionName, ushort connectionHeartbeat)
        {
            _connectionName = connectionName;
            _connectionHeartbeat = connectionHeartbeat;
        }
        
        /// <summary>
        /// Return matching RabbitMQ subscription if exist (match by amqp scheme)
        /// or create new subscription to RabbitMQ (thread-safe)
        /// </summary>
        /// <param name="connectionFactory"></param>
        /// <returns></returns>
        public IConnection GetConnection(ConnectionFactory connectionFactory)
        {
            var connectionId = GetConnectionId(connectionFactory);

            var connectionFound = s_connectionPool.TryGetValue(connectionId, out PooledConnection pooledConnection);

            if (connectionFound && pooledConnection.Connection.IsOpen)
                return pooledConnection.Connection;

            lock (s_lock)
            {
                connectionFound = s_connectionPool.TryGetValue(connectionId, out pooledConnection);

                if (connectionFound == false || pooledConnection.Connection.IsOpen == false)
                {
                    pooledConnection = CreateConnection(connectionFactory);
                }
            }

            return pooledConnection.Connection;
        }

        public void ResetConnection(ConnectionFactory connectionFactory)
        {
            lock (s_lock)
            {
                DelayReconnecting();

                try
                {
                    CreateConnection(connectionFactory);
                }
                catch (BrokerUnreachableException exception)
                {
                    s_logger.LogError(exception,
                        "RmqMessageGatewayConnectionPool: Failed to reset subscription to Rabbit MQ endpoint {URL}",
                        connectionFactory.Endpoint);
                }
            }
        }

        private PooledConnection CreateConnection(ConnectionFactory connectionFactory)
        {
            var connectionId = GetConnectionId(connectionFactory);

            TryRemoveConnection(connectionId);

            s_logger.LogDebug("RmqMessageGatewayConnectionPool: Creating subscription to Rabbit MQ endpoint {URL}", connectionFactory.Endpoint);

            connectionFactory.RequestedHeartbeat = TimeSpan.FromSeconds(_connectionHeartbeat);
            connectionFactory.RequestedConnectionTimeout = TimeSpan.FromMilliseconds(5000);
            connectionFactory.SocketReadTimeout = TimeSpan.FromMilliseconds(5000);
            connectionFactory.SocketWriteTimeout = TimeSpan.FromMilliseconds(5000);

            var connection = connectionFactory.CreateConnection(_connectionName);

            s_logger.LogDebug("RmqMessageGatewayConnectionPool: new connected to {URL} added to pool named {ProviderName}", connection.Endpoint, connection.ClientProvidedName);


            void ShutdownHandler(object sender, ShutdownEventArgs e)
            {
                s_logger.LogWarning("RmqMessageGatewayConnectionPool: The subscription {URL} has been shutdown due to {ErrorMessage}", connection.Endpoint, e.ToString());

                lock (s_lock)
                {
                    TryRemoveConnection(connectionId);
                }
            }

            connection.ConnectionShutdown += ShutdownHandler;

            var pooledConnection = new PooledConnection{Connection = connection, ShutdownHandler = ShutdownHandler};

            s_connectionPool.Add(connectionId, pooledConnection);

            return pooledConnection;
        }

        private void TryRemoveConnection(string connectionId)
        {
            if (s_connectionPool.TryGetValue(connectionId, out PooledConnection pooledConnection))
            {
                    pooledConnection.Connection.ConnectionShutdown -= pooledConnection.ShutdownHandler;
                pooledConnection.Connection.Dispose();
                s_connectionPool.Remove(connectionId);
            }
        }

        private string GetConnectionId(ConnectionFactory connectionFactory)
        {
            return $"{connectionFactory.UserName}.{connectionFactory.Password}.{connectionFactory.HostName}.{connectionFactory.Port}.{connectionFactory.VirtualHost}".ToLowerInvariant();
        }

        private static void DelayReconnecting()
        {
            Task.Delay(jitter.Next(5, 100)).Wait();
        }


        class PooledConnection
        {
            public IConnection Connection { get; set; }
            public EventHandler<ShutdownEventArgs> ShutdownHandler { get; set; }
        }

        public void RemoveConnection(ConnectionFactory connectionFactory)
        {
            var connectionId = GetConnectionId(connectionFactory);

            if (s_connectionPool.ContainsKey(connectionId))
            {
                lock (s_lock)
                {
                    TryRemoveConnection(connectionId);
                }
            }
        }
    }
}
