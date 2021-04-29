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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Polly;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Class ConnectionPolicyFactory.
    /// </summary>
    public class ConnectionPolicyFactory
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<ConnectionPolicyFactory>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPolicyFactory"/> class.
        /// </summary>
        public ConnectionPolicyFactory()
           : this(new RmqMessagingGatewayConnection())
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPolicyFactory"/> class. 
        /// Use if you need to inject a test logger
        /// </summary>
        /// <param name="connection"></param>
        public ConnectionPolicyFactory(RmqMessagingGatewayConnection connection)
        {
            var retries = connection.AmpqUri.ConnectionRetryCount;
            var retryWaitInMilliseconds = connection.AmpqUri.RetryWaitInMilliseconds;
            var circuitBreakerTimeout = connection.AmpqUri.CircuitBreakTimeInMilliseconds;

            RetryPolicy = Policy
                .Handle<BrokerUnreachableException>()
                .Or<Exception>()
                .WaitAndRetry(
                    retries,
                    retryAttempt => TimeSpan.FromMilliseconds(retryWaitInMilliseconds * Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, context) =>
                    {
                        if (exception is BrokerUnreachableException)
                        {
                            s_logger.LogWarning(exception,
                                "RMQMessagingGateway: BrokerUnreachableException error on connecting to queue {ChannelName} exchange {ExchangeName} on subscription {URL}. Will retry {Retries} times",
                                context["queueName"],
                                connection.Exchange.Name,
                                connection.AmpqUri.GetSanitizedUri(),
                                retries);
                        }
                        else
                        {
                            s_logger.LogWarning(exception,
                                "RMQMessagingGateway: Exception on subscription to queue {ChannelName} via exchange {ExchangeName} on subscription {URL}",
                                context["queueName"],
                                connection.Exchange.Name,
                                connection.AmpqUri.GetSanitizedUri());

                            throw exception;
                        }
                    });

            CircuitBreakerPolicy = Policy
                .Handle<BrokerUnreachableException>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(circuitBreakerTimeout));
        }

        /// <summary>
        /// Gets the retry policy.
        /// </summary>
        /// <value>The retry policy.</value>
        public Policy RetryPolicy { get; }

        /// <summary>
        /// Gets the circuit breaker policy.
        /// </summary>
        /// <value>The circuit breaker policy.</value>
        public Policy CircuitBreakerPolicy { get; }
    }
}
