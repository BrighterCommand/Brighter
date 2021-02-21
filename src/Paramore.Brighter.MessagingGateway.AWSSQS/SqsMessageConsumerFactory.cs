// ***********************************************************************
// Assembly         : paramore.brighter.messaginggateway.awssqs
// Author           : ian
// Created          : 08-17-2015
//
// Last Modified By : ian
// Last Modified On : 10-25-2015
// ***********************************************************************
// <copyright file="SqsMessageConsumerFactory.cs" company="">
//     Copyright ©  2015
// </copyright>
// <summary></summary>
// ***********************************************************************

using Amazon;
using Amazon.Runtime;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// Class SqsMessageConsumerFactory.
    /// </summary>
    public class SqsMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly AWSMessagingGatewayConnection _awsConnection;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageConsumerFactory"/> class.
        /// </summary>
        public SqsMessageConsumerFactory(AWSMessagingGatewayConnection awsConnection)
        {
            _awsConnection = awsConnection;
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumer.</returns>
        public IAmAMessageConsumer Create(Subscription subscription)
        {
            SqsSubscription sqsSubscription = subscription as SqsSubscription;
            if (sqsSubscription == null) throw new ConfigurationException("We expect an SqsSubscription or SqsSubscription<T> as a parameter");
            
            return new SqsMessageConsumer(
                awsConnection: _awsConnection, 
                queueName:subscription.ChannelName.ToValidSQSQueueName(), 
                routingKey:subscription.RoutingKey, 
                batchSize: subscription.BufferSize,
                hasDLQ: sqsSubscription.RedrivePolicy == null
                );
        }
    }
}
