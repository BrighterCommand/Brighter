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
        static Lazy<ContextualPolicy> retryPolicy;
        static Lazy<Policy> circuitBreakerPolicy;

        public ConnectionPolicyFactory(ILog logger)
        {
            this.logger = logger;

            var configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            int retries = configuration.AMPQUri.ConnectionRetryCount;
            int retryWait = configuration.AMPQUri.RetryWaitInMilliseconds;

            retryPolicy = new Lazy<ContextualPolicy>(() => 
                Policy
                .Handle<BrokerUnreachableException>()
                .Or<Exception>()
                .WaitAndRetry(
                    retries,
                    retryAttempt => TimeSpan.FromSeconds(retryWait),
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

                    })
              );

            //TODO: Configure the break timespan
            circuitBreakerPolicy = new Lazy<Policy>(() =>
                Policy
                .Handle<BrokerUnreachableException>()
                .CircuitBreaker(1, TimeSpan.FromMinutes(1)));

        }

        public ContextualPolicy RetryPolicy
        {
            get { return retryPolicy.Value; }
        }

        public Policy CircuitBreakerPolicy
        {
            get { return circuitBreakerPolicy.Value; }
        }
    }
}
