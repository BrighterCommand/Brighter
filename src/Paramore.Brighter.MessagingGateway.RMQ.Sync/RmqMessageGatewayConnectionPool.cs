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
using System.Threading;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
{
    /// <summary>
    /// Shares RabbitMQ connections across gateways using the same broker credentials and virtual host.
    /// </summary>
    /// <remarks>
    /// Shutdown notifications do not evict connections, allowing client automatic recovery to run.
    /// A lookup that finds a closed connection disposes it and creates a replacement instead of waiting
    /// for recovery. Reset, removal and release of the last gateway reference also dispose connections.
    /// </remarks>
    public partial class RmqMessageGatewayConnectionPool
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
        /// Returns an open pooled connection or replaces a closed or missing connection (thread-safe).
        /// </summary>
        /// <param name="connectionFactory">The factory identifying the broker connection and used to create it.</param>
        /// <returns>The pooled connection.</returns>
        /// <remarks>The returned connection is borrowed and does not acquire a gateway reference.
        /// Pool reset, removal or gateway disposal can close it. Replacement of a closed connection
        /// cancels any automatic recovery in progress on that connection.</remarks>
        public IConnection? GetConnection(ConnectionFactory connectionFactory) => GetConnection(connectionFactory, false);

        internal IConnection? AcquireConnection(ConnectionFactory connectionFactory) => GetConnection(connectionFactory, true);

        private IConnection? GetConnection(ConnectionFactory connectionFactory, bool acquire)
        {
            var connectionId = GetConnectionId(connectionFactory);

            lock (s_lock)
            {
                var connectionFound = s_connectionPool.TryGetValue(connectionId, out PooledConnection? pooledConnection);

                if (connectionFound == false || pooledConnection!.Connection!.IsOpen == false)
                {
                    pooledConnection = CreateConnection(connectionFactory);
                }

                if (acquire) pooledConnection.ReferenceCount++;

                return pooledConnection.Connection;
            }
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
                    Log.FailedToResetSubscriptionToRabbitMqEndpoint(s_logger, connectionFactory.Endpoint, exception);
                }
            }
        }

        internal void ReleaseConnection(ConnectionFactory connectionFactory, IConnection connection)
        {
            var connectionId = GetConnectionId(connectionFactory);
            lock (s_lock)
            {
                if (!s_connectionPool.TryGetValue(connectionId, out var pooledConnection)) return;

                if (ReferenceEquals(pooledConnection.Connection, connection)) pooledConnection.ReferenceCount--;
                if (pooledConnection.ReferenceCount == 0)
                {
                    TryRemoveConnection(connectionId);
                }
            }
        }

        private PooledConnection CreateConnection(ConnectionFactory connectionFactory)
        {
            var connectionId = GetConnectionId(connectionFactory);

            TryRemoveConnection(connectionId);

            Log.CreatingSubscriptionToRabbitMqEndpoint(s_logger, connectionFactory.Endpoint);

            connectionFactory.RequestedHeartbeat = TimeSpan.FromSeconds(_connectionHeartbeat);
            connectionFactory.RequestedConnectionTimeout = TimeSpan.FromMilliseconds(5000);
            connectionFactory.SocketReadTimeout = TimeSpan.FromMilliseconds(5000);
            connectionFactory.SocketWriteTimeout = TimeSpan.FromMilliseconds(5000);

            var connection = connectionFactory.CreateConnection(_connectionName);

            Log.NewConnectedToAddedToPool(s_logger, connection.Endpoint, connection.ClientProvidedName);

            var pooledConnection = new PooledConnection { Connection = connection };

            s_connectionPool.Add(connectionId, pooledConnection);

            return pooledConnection;
        }

        private void TryRemoveConnection(string connectionId)
        {
            if (!s_connectionPool.TryGetValue(connectionId, out PooledConnection? pooledConnection)) return;

            s_connectionPool.Remove(connectionId);
            //netstandard20 issue, if connectionfound is true, pooledConnection is not null
            pooledConnection.Connection!.Dispose();
        }

        private string GetConnectionId(ConnectionFactory connectionFactory)
        {
            return $"{connectionFactory.UserName}.{connectionFactory.Password}.{connectionFactory.HostName}.{connectionFactory.Port}.{connectionFactory.VirtualHost}".ToLowerInvariant();
        }

        private static void DelayReconnecting()
        {
            Thread.Sleep(jitter.Next(5, 100));
        }


        sealed class PooledConnection
        {
            public int ReferenceCount { get; set; }
            public IConnection? Connection { get; set; }
        }

        public void RemoveConnection(ConnectionFactory connectionFactory)
        {
            var connectionId = GetConnectionId(connectionFactory);

            lock (s_lock)
            {
                TryRemoveConnection(connectionId);
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Error, "RmqMessageGatewayConnectionPool: Failed to reset subscription to Rabbit MQ endpoint {Url}")]
            public static partial void FailedToResetSubscriptionToRabbitMqEndpoint(ILogger logger, AmqpTcpEndpoint url, Exception exception);

            [LoggerMessage(LogLevel.Debug, "RmqMessageGatewayConnectionPool: Creating subscription to Rabbit MQ endpoint {Url}")]
            public static partial void CreatingSubscriptionToRabbitMqEndpoint(ILogger logger, AmqpTcpEndpoint url);

            [LoggerMessage(LogLevel.Debug, "RmqMessageGatewayConnectionPool: new connected to {Url} added to pool named {ProviderName}")]
            public static partial void NewConnectedToAddedToPool(ILogger logger, AmqpTcpEndpoint url, string providerName);
        }
    }
}

