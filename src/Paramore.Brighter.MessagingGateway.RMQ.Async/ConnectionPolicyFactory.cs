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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async
{
    /// <summary>
    /// Class ConnectionPolicyFactory.
    /// </summary>
    public partial class ConnectionPolicyFactory
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPolicyFactory"/> class.
        /// </summary>
        public ConnectionPolicyFactory(ILoggerFactory? loggerFactory = null)
           : this(new RmqMessagingGatewayConnection(), loggerFactory)
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPolicyFactory"/> class.
        /// Use if you need to inject a test logger
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> used to create a logger; defaults to <see cref="NullLoggerFactory"/></param>
        public ConnectionPolicyFactory(RmqMessagingGatewayConnection connection, ILoggerFactory? loggerFactory = null)
        {
            _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ConnectionPolicyFactory>();

            if (connection.Exchange is null) throw new ConfigurationException("RabbitMQ Exchange is not set");
            if (connection.AmpqUri is null) throw new ConfigurationException("RabbitMQ Broker URL is not set");
            
            var retries = connection.AmpqUri.ConnectionRetryCount;
            var retryWaitInMilliseconds = connection.AmpqUri.RetryWaitInMilliseconds;
            var circuitBreakerTimeout = connection.AmpqUri.CircuitBreakTimeInMilliseconds;

            RetryPolicyAsync = Policy
                .Handle<BrokerUnreachableException>()
                .Or<Exception>()
                .WaitAndRetryAsync(
                    retries,
                    retryAttempt => TimeSpan.FromMilliseconds(retryWaitInMilliseconds * Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, context) =>
                    {
                        if (exception is BrokerUnreachableException)
                        {
                            Log.BrokerUnreachableException(_logger, exception, context["queueName"].ToString(), connection.Exchange.Name, connection.AmpqUri.GetSanitizedUri(), retries);
                        }
                        else
                        {
                            Log.ExceptionOnSubscription(_logger, exception, context["queueName"].ToString(), connection.Exchange.Name, connection.AmpqUri.GetSanitizedUri());

                            throw new ChannelFailureException($"RMQMessagingGateway: Exception on subscription to queue { context["queueName"]} via exchange {connection.Exchange.Name} on subscription {connection.AmpqUri.GetSanitizedUri()}", exception);
                        }
                    });

            CircuitBreakerPolicyAsync = Policy
                .Handle<BrokerUnreachableException>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(circuitBreakerTimeout));
        }

        /// <summary>
        /// Gets the retry policy.
        /// </summary>
        /// <value>The retry policy.</value>
        public AsyncPolicy RetryPolicyAsync { get; }

        /// <summary>
        /// Gets the circuit breaker policy.
        /// </summary>
        /// <value>The circuit breaker policy.</value>
        public AsyncPolicy CircuitBreakerPolicyAsync { get; }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "RMQMessagingGateway: BrokerUnreachableException error on connecting to queue {ChannelName} exchange {ExchangeName} on subscription {Url}. Will retry {Retries} times")]
            public static partial void BrokerUnreachableException(ILogger logger, Exception exception, string? channelName, string exchangeName, string url, int retries);

            [LoggerMessage(LogLevel.Warning, "RMQMessagingGateway: Exception on subscription to queue {ChannelName} via exchange {ExchangeName} on subscription {Url}")]
            public static partial void ExceptionOnSubscription(ILogger logger, Exception exception, string? channelName, string exchangeName, string url);
        }
    }
}

