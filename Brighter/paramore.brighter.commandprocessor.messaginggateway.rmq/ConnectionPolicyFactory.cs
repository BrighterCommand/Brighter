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
    class ConnectionPolicyFactory
    {
        readonly ILog logger;

        public ConnectionPolicyFactory(ILog logger)
        {
            this.logger = logger;

            var configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            int retries = configuration.AMPQUri.ConnectionRetryCount;
            int retryWait = configuration.AMPQUri.RetryWaitInMilliseconds;
            int circuitBreakerTimeout = configuration.AMPQUri.CircuitBreakTimeInMilliseconds;

            RetryPolicy =
                Policy
                    .Handle<BrokerUnreachableException>()
                    .Or<Exception>()
                    .WaitAndRetry(
                        retries,
                        retryAttempt => TimeSpan.FromMilliseconds(retryWait),
                        (exception, retryCount, context) =>
                        {
                            if (exception is BrokerUnreachableException)
                            {
                                logger.WarnException(
                                    "RMQMessagingGateway: Error on connecting to queue {0} exchange {1} on connection {2}. Will retry {3} times, this is the {4} attempt",
                                    exception,
                                    configuration.Exchange.Name,
                                    configuration.AMPQUri.GetSantizedUri(),
                                    retries,
                                    retryCount
                                    );
                            }
                            else
                            {
                                logger.WarnException(
                                    "RMQMessagingGateway: Exception on connection to queue {0} via exchange {1} on connection {2}",
                                    exception,
                                    context["queueName"],
                                    configuration.Exchange.Name,
                                    configuration.AMPQUri.GetSantizedUri()
                                    );
                                throw exception;
                            }

                        });

            //TODO: Configure the break timespan
            CircuitBreakerPolicy = 
                Policy
                .Handle<BrokerUnreachableException>()
                .CircuitBreaker(1, TimeSpan.FromMinutes(circuitBreakerTimeout));

        }

        public ContextualPolicy RetryPolicy { get; private set; }

        public Policy CircuitBreakerPolicy { get; private set; }
    }
}
