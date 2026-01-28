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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

/// <summary>
///     Class RmqMessageGateway.
///     Base class for messaging gateway used by a <see cref="Brighter.Channel" /> to communicate with a RabbitMQ server,
///     to consume messages from the server or
///     <see cref="CommandProcessor.Post{T}" /> to send a message to the RabbitMQ server.
///     A channel is associated with a queue name, which binds to a <see cref="MessageHeader.Topic" /> when
///     <see cref="CommandProcessor.Post{T}" /> sends over a task queue.
///     So to listen for messages on that Topic you need to bind to the matching queue name.
///     The configuration holds a <serviceActivatorConnections> section which in turn contains a <connections>
///     collection that contains a set of connections.
///     Each subscription identifies a mapping between a queue name and a <see cref="IRequest" /> derived type. At runtime we
///     read this list and listen on the associated channels.
///     The Message Pump (Reactor or Proactor) then uses the <see cref="IAmAMessageMapper" /> associated with the configured
///     request type in <see cref="IAmAMessageMapperRegistry" /> to translate between the
///     on-the-wire message and the <see cref="Command" /> or <see cref="Event" />
/// </summary>
public partial class RmqMessageGateway : IDisposable, IAsyncDisposable
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageGateway>();
    private readonly AsyncPolicy _circuitBreakerPolicy;
    private readonly ConnectionFactory _connectionFactory;
    private readonly AsyncPolicy _retryPolicy;
    protected readonly RmqMessagingGatewayConnection Connection;
    protected IChannel? Channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="RmqMessageGateway" /> class.
    ///  Use if you need to inject a test logger
    /// </summary>
    /// <param name="connection">The amqp uri and exchange to connect to</param>
    protected RmqMessageGateway(RmqMessagingGatewayConnection connection)
    {
        Connection = connection;

        var connectionPolicyFactory = new ConnectionPolicyFactory(Connection);

        _retryPolicy = connectionPolicyFactory.RetryPolicyAsync;
        _circuitBreakerPolicy = connectionPolicyFactory.CircuitBreakerPolicyAsync;

       if (Connection.AmpqUri is null) throw new ConfigurationException("RMQMessagingGateway: No AMPQ URI specified");

        _connectionFactory = new ConnectionFactory
        {
            Uri = Connection.AmpqUri.Uri,
            RequestedHeartbeat = TimeSpan.FromSeconds(connection.Heartbeat),
            ContinuationTimeout = TimeSpan.FromSeconds(connection.ContinuationTimeout)
        };

        // Configure SSL/TLS for mutual authentication if certificate is provided
        RmqTlsConfigurator.ConfigureIfEnabled(_connectionFactory, connection);

        if (Connection.Exchange is null) throw new InvalidOperationException("RMQMessagingGateway: No Exchange specified");

        DelaySupported = Connection.Exchange.SupportDelay;
    }

    /// <summary>
    ///     Gets if the current provider configuration is able to support delayed delivery of messages.
    /// </summary>
    public bool DelaySupported { get; }

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Connects the specified queue name.
    /// </summary>
    /// <param name="queueName">Name of the queue. For producer use default of "Producer Channel". Passed to Polly for debugging</param>
    /// <param name="makeExchange">Do we create the exchange if it does not exist</param>
    /// <param name="cancellationToken">Cancel the operation</param>
    /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
    protected async Task EnsureBrokerAsync(
        ChannelName? queueName = null,
        OnMissingChannel makeExchange = OnMissingChannel.Create,
        CancellationToken cancellationToken = default
    )
    {
        queueName ??= new ChannelName("Producer Channel");

        await ConnectWithCircuitBreakerAsync(queueName, makeExchange, cancellationToken);
    }

    private async Task ConnectWithCircuitBreakerAsync(ChannelName queueName, OnMissingChannel makeExchange, CancellationToken cancellationToken = default)
    {
        await _circuitBreakerPolicy.ExecuteAsync(() => ConnectWithRetryAsync(queueName, makeExchange, cancellationToken));
    }

    private async Task ConnectWithRetryAsync(ChannelName queueName, OnMissingChannel makeExchange, CancellationToken cancellationToken = default)
    {
        await _retryPolicy.ExecuteAsync(async _ => await ConnectToBrokerAsync(makeExchange, cancellationToken),
            new Dictionary<string, object> { { "queueName", queueName.Value } });
    }

    protected virtual async Task ConnectToBrokerAsync(OnMissingChannel makeExchange, CancellationToken cancellationToken = default)
    {
        if (Channel == null || Channel.IsClosed)
        {
            var connection = await new RmqMessageGatewayConnectionPool(Connection.Name, Connection.Heartbeat)
                .GetConnectionAsync(_connectionFactory, cancellationToken);

           if (Connection.AmpqUri is null) throw new ConfigurationException("RMQMessagingGateway: No AMPQ URI specified");

            connection.ConnectionBlockedAsync += HandleBlockedAsync;
            connection.ConnectionUnblockedAsync += HandleUnBlockedAsync;

            Log.OpeningChannelToRabbitMq(s_logger, Connection.AmpqUri.GetSanitizedUri());

            Channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true),
                cancellationToken);

            //desired state configuration of the exchange
            await Channel.DeclareExchangeForConnection(Connection, makeExchange, cancellationToken: cancellationToken);
        }
    }

    private Task HandleBlockedAsync(object sender, ConnectionBlockedEventArgs args)
    {
       if (Connection.AmpqUri is null) throw new ConfigurationException("RMQMessagingGateway: No AMPQ URI specified");

        Log.SubscriptionBlocked(s_logger, Connection.AmpqUri.GetSanitizedUri(), args.Reason);

        return Task.CompletedTask;
    }

    private Task HandleUnBlockedAsync(object sender, AsyncEventArgs args)
    {
       if (Connection.AmpqUri is null) throw new ConfigurationException("RMQMessagingGateway: No AMPQ URI specified");

        Log.SubscriptionUnblocked(s_logger, Connection.AmpqUri.GetSanitizedUri());
        return Task.CompletedTask;
    }

    protected async Task ResetConnectionToBrokerAsync(CancellationToken cancellationToken = default)
    {
        await new RmqMessageGatewayConnectionPool(Connection.Name, Connection.Heartbeat).ResetConnectionAsync(_connectionFactory, cancellationToken);
    }

    ~RmqMessageGateway()
    {
        Dispose(false);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (Channel != null)
        {
            await Channel.AbortAsync();
            await Channel.DisposeAsync();
            Channel = null;
        }

        await new RmqMessageGatewayConnectionPool(Connection.Name, Connection.Heartbeat).RemoveConnectionAsync(_connectionFactory);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Channel?.AbortAsync().Wait();
            Channel?.Dispose();
            Channel = null;

            new RmqMessageGatewayConnectionPool(Connection.Name, Connection.Heartbeat).RemoveConnectionAsync(_connectionFactory)
                .GetAwaiter()
                .GetResult();
        }
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "RMQMessagingGateway: Subscription to {URL} blocked. Reason: {ErrorMessage}")]
        public static partial void SubscriptionBlocked(ILogger logger, string url, string errorMessage);

        [LoggerMessage(LogLevel.Information, "RMQMessagingGateway: Subscription to {URL} unblocked")]
        public static partial void SubscriptionUnblocked(ILogger logger, string url);

        [LoggerMessage(LogLevel.Debug, "RMQMessagingGateway: Opening channel to Rabbit MQ on {URL}")]
        public static partial void OpeningChannelToRabbitMq(ILogger logger, string url);
    }
}

