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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
{
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
    ///     The <see cref="Sync" /> then uses the <see cref="IAmAMessageMapper" /> associated with the configured
    ///     request type in <see cref="IAmAMessageMapperRegistry" /> to translate between the
    ///     on-the-wire message and the <see cref="Command" /> or <see cref="Event" />
    /// </summary>
    public partial class RmqMessageGateway : IDisposable
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageGateway>();
        private readonly Policy _circuitBreakerPolicy;
        private readonly ConnectionFactory _connectionFactory;
        private readonly Policy _retryPolicy;
        protected readonly RmqMessagingGatewayConnection Connection;
        protected IModel? Channel;

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageGateway" /> class.
        ///  Use if you need to inject a test logger
        /// </summary>
        /// <param name="connection">The amqp uri and exchange to connect to</param>
        protected RmqMessageGateway(RmqMessagingGatewayConnection connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            
            if (Connection.AmpqUri is null)
                throw new InvalidOperationException("RMQMessagingGateway: Connection must have an AMPQ URI");
            
            if (Connection.Exchange is null)
                throw new InvalidOperationException("RMQMessagingGateway: Connection must have an Exchange");

            var connectionPolicyFactory = new ConnectionPolicyFactory(Connection);

            _retryPolicy = connectionPolicyFactory.RetryPolicy;
            _circuitBreakerPolicy = connectionPolicyFactory.CircuitBreakerPolicy;

            _connectionFactory = new ConnectionFactory
            {
                Uri = Connection.AmpqUri.Uri,
                RequestedHeartbeat = TimeSpan.FromSeconds(connection.Heartbeat),
                ContinuationTimeout = TimeSpan.FromSeconds(connection.ContinuationTimeout)
            };

            // Configure SSL/TLS for mutual authentication if certificate is provided
            RmqTlsConfigurator.ConfigureIfEnabled(_connectionFactory, connection);

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
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        protected void EnsureBroker(ChannelName? queueName = null, OnMissingChannel makeExchange = OnMissingChannel.Create)
        {
            queueName ??= new ChannelName("Producer Channel");
            
            ConnectWithCircuitBreaker(queueName, makeExchange);
        }

        private void ConnectWithCircuitBreaker(ChannelName queueName, OnMissingChannel makeExchange)
        {
            _circuitBreakerPolicy.Execute(() => ConnectWithRetry(queueName, makeExchange));
        }

        private void ConnectWithRetry(ChannelName queueName, OnMissingChannel makeExchange)
        {
            _retryPolicy.Execute((_) => ConnectToBroker(makeExchange), new Dictionary<string, object> {{"queueName", queueName.Value}});
        }

        protected virtual void ConnectToBroker(OnMissingChannel makeExchange)
        {
            if (Channel == null || Channel.IsClosed)
            {
                if (Connection.Name is null)
                    throw new InvalidOperationException("RMQMessagingGateway: Connection must have a name");
                
                if (Connection.AmpqUri is null)
                    throw new InvalidOperationException("RMQMessagingGateway: Connection must have an AMPQ URI");
                
                var connection = new RmqMessageGatewayConnectionPool(Connection.Name, Connection.Heartbeat).GetConnection(_connectionFactory);
                
                if (connection is null)
                    throw new InvalidOperationException($"RMQMessagingGateway: Connection to {Connection.AmpqUri.GetSanitizedUri()} failed" );

                connection.ConnectionBlocked += HandleBlocked;
                connection.ConnectionUnblocked += HandleUnBlocked;

                Log.OpeningChannelToRabbitMq(s_logger, Connection.AmpqUri.GetSanitizedUri());

                Channel = connection.CreateModel();

                //desired state configuration of the exchange
                Channel.DeclareExchangeForConnection(Connection, makeExchange);
            }
        }

        private void HandleBlocked(object? sender, ConnectionBlockedEventArgs args)
        {
            Log.SubscriptionBlocked(s_logger, Connection.AmpqUri!.GetSanitizedUri(), args.Reason);
        }

        private void HandleUnBlocked(object? sender, EventArgs args)
        { 
            Log.SubscriptionUnblocked(s_logger, Connection.AmpqUri!.GetSanitizedUri());
        }

        protected void ResetConnectionToBroker()
        {
            if (Connection.Name is null)
                throw new InvalidOperationException("RMQMessagingGateway: Connection must have a name");

            new RmqMessageGatewayConnectionPool(Connection.Name, Connection.Heartbeat).ResetConnection(_connectionFactory);
        }

        ~RmqMessageGateway()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {

                Channel?.Abort();
                Channel?.Dispose();
                Channel = null;

                if (Connection.Name is not null)
                    new RmqMessageGatewayConnectionPool(Connection.Name, Connection.Heartbeat).RemoveConnection(_connectionFactory);
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "RMQMessagingGateway: Opening channel to Rabbit MQ on {URL}")]
            public static partial void OpeningChannelToRabbitMq(ILogger logger, string url);

            [LoggerMessage(LogLevel.Warning, "RMQMessagingGateway: Subscription to {URL} blocked. Reason: {ErrorMessage}")]
            public static partial void SubscriptionBlocked(ILogger logger, string url, string errorMessage);

            [LoggerMessage(LogLevel.Information, "RMQMessagingGateway: Subscription to {URL} unblocked")]
            public static partial void SubscriptionUnblocked(ILogger logger, string url);
        }
    }
}

