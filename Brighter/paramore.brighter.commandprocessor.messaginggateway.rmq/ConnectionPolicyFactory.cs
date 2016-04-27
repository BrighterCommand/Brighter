// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 02-16-2015
//
// Last Modified By : ian
// Last Modified On : 02-26-2015
// ***********************************************************************
// <copyright file="ConnectionPolicyFactory.cs" company="">
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

using System;

using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;
using Polly;
using RabbitMQ.Client.Exceptions;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class ConnectionPolicyFactory.
    /// </summary>
    public class ConnectionPolicyFactory
    {
        private readonly ILog _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPolicyFactory"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public ConnectionPolicyFactory()
           : this(LogProvider.GetCurrentClassLogger(), RMQMessagingGatewayConfigurationSection.GetConfiguration())
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPolicyFactory"/> class. 
        /// Use if you need to inject a test logger
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="configurationSection"></param>
        public ConnectionPolicyFactory(ILog logger, RMQMessagingGatewayConfigurationSection configurationSection)
        {
            _logger = logger;

            var configuration = configurationSection;
            int retries = configuration.AMPQUri.ConnectionRetryCount;
            int retryWaitInMilliseconds = configuration.AMPQUri.RetryWaitInMilliseconds;
            int circuitBreakerTimeout = configuration.AMPQUri.CircuitBreakTimeInMilliseconds;

            RetryPolicy = Policy.Handle<BrokerUnreachableException>()
                .Or<Exception>()
                .WaitAndRetry(
                    retries,
                    retryAttempt => TimeSpan.FromMilliseconds(retryWaitInMilliseconds * Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, context) =>
                    {
                        if (exception is BrokerUnreachableException)
                        {
                            _logger.WarnException(
                                "RMQMessagingGateway: BrokerUnreachableException error on connecting to queue {0} exchange {1} on connection {2}. Will retry {3} times",
                                exception,
                                context["queueName"],
                                configuration.Exchange.Name,
                                configuration.AMPQUri.GetSanitizedUri(),
                                retries);
                        }
                        else
                        {
                            logger.WarnException(
                                "RMQMessagingGateway: Exception on connection to queue {0} via exchange {1} on connection {2}",
                                exception,
                                context["queueName"],
                                configuration.Exchange.Name,
                                configuration.AMPQUri.GetSanitizedUri());
                            throw exception;
                        }
                    });

            CircuitBreakerPolicy = Policy.Handle<BrokerUnreachableException>().CircuitBreaker(1, TimeSpan.FromMilliseconds(circuitBreakerTimeout));
        }

        /// <summary>
        /// Gets the retry policy.
        /// </summary>
        /// <value>The retry policy.</value>
        public ContextualPolicy RetryPolicy { get; private set; }
        /// <summary>
        /// Gets the circuit breaker policy.
        /// </summary>
        /// <value>The circuit breaker policy.</value>
        public Policy CircuitBreakerPolicy { get; private set; }
    }
}
