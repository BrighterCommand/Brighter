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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

/// <summary>
/// Class MessageGatewayConnectionPool.
/// </summary>
public partial class RmqMessageGatewayConnectionPool(string connectionName, ushort connectionHeartbeat)
{
    private static readonly Dictionary<string, PooledConnection> s_connectionPool = new();

    private static readonly SemaphoreSlim s_lock = new SemaphoreSlim(1, 1);
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageGatewayConnectionPool>();
    private static readonly Random jitter = new Random();

    /// <summary>
    /// Return matching RabbitMQ subscription if exist (match by amqp scheme)
    /// or create new subscription to RabbitMQ (thread-safe)
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    public IConnection GetConnection(ConnectionFactory connectionFactory) => BrighterAsyncContext.Run(() => GetConnectionAsync(connectionFactory));

    /// <summary>
    /// Return matching RabbitMQ subscription if exist (match by amqp scheme)
    /// or create new subscription to RabbitMQ (thread-safe)
    /// </summary>
    /// <param name="connectionFactory">A <see cref="ConnectionFactory"/> to create new connections</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation</param>
    /// <returns></returns>
    public async Task<IConnection> GetConnectionAsync(ConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        var connectionId = GetConnectionId(connectionFactory);

        await s_lock.WaitAsync(cancellationToken);

        try
        {
            var connectionFound = s_connectionPool.TryGetValue(connectionId, out PooledConnection? pooledConnection);

            if (connectionFound == false || pooledConnection!.Connection.IsOpen == false)
            {
                pooledConnection = await CreateConnectionAsync(connectionFactory, cancellationToken);
            }

            return pooledConnection.Connection;
        }
        finally
        {
            s_lock.Release();
        }
    }

      public async Task ResetConnectionAsync(ConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
      {
          await s_lock.WaitAsync(cancellationToken);

          try
          {
              await DelayReconnectingAsync();

              try
              {
                  await CreateConnectionAsync(connectionFactory, cancellationToken);
              }
              catch (BrokerUnreachableException exception)
              {
                  Log.FailedToResetSubscriptionToRabbitMqEndpoint(s_logger, connectionFactory.Endpoint, exception);
              }
          }
          finally
          {
              s_lock.Release();
          }
      }
    
    /// <summary>
    /// Remove the connection from the pool
    /// </summary>
    /// <param name="connectionFactory">The factory that creates broker connections</param>
    public async Task RemoveConnectionAsync(ConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        var connectionId = GetConnectionId(connectionFactory);

        if (s_connectionPool.ContainsKey(connectionId))
        {
            await s_lock.WaitAsync(cancellationToken);
            try
            {
                await TryRemoveConnectionAsync(connectionId);
            }
            finally
            {
                s_lock.Release();
            }
        }
    }

    private async Task<PooledConnection> CreateConnectionAsync(ConnectionFactory connectionFactory, CancellationToken cancellationToken = default)
    {
        var connectionId = GetConnectionId(connectionFactory);

        await TryRemoveConnectionAsync(connectionId);

        Log.CreatingSubscriptionToRabbitMqEndpoint(s_logger, connectionFactory.Endpoint);

        connectionFactory.RequestedHeartbeat = TimeSpan.FromSeconds(connectionHeartbeat);
        connectionFactory.RequestedConnectionTimeout = TimeSpan.FromMilliseconds(5000);
        connectionFactory.SocketReadTimeout = TimeSpan.FromMilliseconds(5000);
        connectionFactory.SocketWriteTimeout = TimeSpan.FromMilliseconds(5000);

        var connection = await connectionFactory.CreateConnectionAsync(connectionName, cancellationToken);

        Log.NewConnectedToAddedToPool(s_logger, connection.Endpoint, connection.ClientProvidedName);


        async Task ShutdownHandler(object sender, ShutdownEventArgs e)
        {
            Log.SubscriptionHasBeenShutdown(s_logger, connection.Endpoint, e.ToString());

            await s_lock.WaitAsync(e.CancellationToken);

            try
            {
                await TryRemoveConnectionAsync(connectionId);
            }
            finally
            {
                s_lock.Release();
            }
        }

        connection.ConnectionShutdownAsync += ShutdownHandler;

        var pooledConnection = new PooledConnection(connection, ShutdownHandler);

        s_connectionPool.Add(connectionId, pooledConnection);

        return pooledConnection;
    }
    
    private static async Task DelayReconnectingAsync() => await Task.Delay(jitter.Next(5, 100));
    
    private async Task TryRemoveConnectionAsync(string connectionId)
    {
        if (s_connectionPool.TryGetValue(connectionId, out PooledConnection? pooledConnection))
        {
            pooledConnection.Connection.ConnectionShutdownAsync -= pooledConnection.ShutdownHandler;
            await pooledConnection.Connection.DisposeAsync();
            s_connectionPool.Remove(connectionId);
        }
    }

    private static string GetConnectionId(ConnectionFactory connectionFactory)
        =>
            $"{connectionFactory.UserName}.{connectionFactory.Password}.{connectionFactory.HostName}.{connectionFactory.Port}.{connectionFactory.VirtualHost}"
                .ToLowerInvariant();

    private sealed record PooledConnection(IConnection Connection, AsyncEventHandler<ShutdownEventArgs> ShutdownHandler);

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Error, "RmqMessageGatewayConnectionPool: Failed to reset subscription to Rabbit MQ endpoint {URL}")]
        public static partial void FailedToResetSubscriptionToRabbitMqEndpoint(ILogger logger, AmqpTcpEndpoint url, Exception exception);

        [LoggerMessage(LogLevel.Debug, "RmqMessageGatewayConnectionPool: Creating subscription to Rabbit MQ endpoint {URL}")]
        public static partial void CreatingSubscriptionToRabbitMqEndpoint(ILogger logger, AmqpTcpEndpoint url);

        [LoggerMessage(LogLevel.Debug, "RmqMessageGatewayConnectionPool: new connected to {URL} added to pool named {ProviderName}")]
        public static partial void NewConnectedToAddedToPool(ILogger logger, AmqpTcpEndpoint url, string? providerName);

        [LoggerMessage(LogLevel.Warning, "RmqMessageGatewayConnectionPool: The subscription {URL} has been shutdown due to {ErrorMessage}")]
        public static partial void SubscriptionHasBeenShutdown(ILogger logger, AmqpTcpEndpoint url, string errorMessage);
    }
}

