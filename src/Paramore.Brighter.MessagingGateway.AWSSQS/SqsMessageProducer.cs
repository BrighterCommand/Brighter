// ***********************************************************************
// Assembly         : paramore.brighter.messaginggateway.awssqs
// Author           : ian
// Created          : 08-17-2015
//
// Last Modified By : ian
// Last Modified On : 10-25-2015
// ***********************************************************************
// <copyright file="SqsMessageProducer.cs" company="">
//     Copyright ©  2015
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;
using Amazon.SimpleNotificationService;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// Class SqsMessageProducer.
    /// </summary>
    public class SqsMessageProducer : AWSMessagingGateway, IAmAMessageProducer
    {
        private readonly AWSMessagingGatewayConnection _connection;
        private readonly string _topicArn;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageProducer"/> class.
        /// <param name="connection">How do we connect to AWS in order to manage middleware</param>
         /// </summary>
        public SqsMessageProducer(AWSMessagingGatewayConnection connection)
            :this(connection, new SqsPublication{MakeChannels = OnMissingChannel.Create})
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageProducer"/> class.
        /// </summary>
        /// <param name="connection">How do we connect to AWS in order to manage middleware</param>
        /// <param name="publication">Configuration of a producer</param>
        public SqsMessageProducer(AWSMessagingGatewayConnection connection, SqsPublication publication)
            : base(connection)
        {
            _connection = connection;
            _topicArn = EnsureTopic(new RoutingKey(publication.RoutingKey), publication.SnsAttributes, publication.MakeChannels);
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Send(Message message)
        {
            _logger.Value.DebugFormat("SQSMessageProducer: Publishing message with topic {0} and id {1} and message: {2}", 
                message.Header.Topic, message.Id, message.Body);

            using (var client = new AmazonSimpleNotificationServiceClient(_connection.Credentials, _connection.Region))
            {
                var publisher = new SqsMessagePublisher(_topicArn, client);
                publisher.Publish(message);
            }
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">The sending delay</param>
        /// <returns>Task.</returns>
        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            Send(message);
        }
        

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            
        }
    }
}
