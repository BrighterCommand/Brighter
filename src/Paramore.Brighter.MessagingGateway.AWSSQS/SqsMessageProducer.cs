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
using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Newtonsoft.Json;
using Paramore.Brighter.MessagingGateway.AWSSQS.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// Class SqsMessageProducer.
    /// </summary>
    public class SqsMessageProducer : IAmAMessageProducer
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<SqsMessageProducer>);

        private readonly AWSCredentials _credentials;
        private readonly RegionEndpoint _regionEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageProducer"/> class.
        /// </summary>
        /// <param name="credentials">The credentials for the AWS account being used</param>
        public SqsMessageProducer(AWSCredentials credentials) 
        {
            _credentials = credentials;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageProducer"/> class.
        /// </summary>
        /// <param name="credentials">The credentials for the AWS account being used</param>
        /// <param name="regionEndpoint">The AWS region used to connect to the SNS.</param>
        public SqsMessageProducer(AWSCredentials credentials, RegionEndpoint regionEndpoint) : this(credentials)
        {
            _regionEndpoint = regionEndpoint;
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Send(Message message)
        {
            var messageString = JsonConvert.SerializeObject(message);
            _logger.Value.DebugFormat("SQSMessageProducer: Publishing message with topic {0} and id {1} and message: {2}", message.Header.Topic, message.Id, messageString);

            using (var client = new AmazonSimpleNotificationServiceClient(_credentials, _regionEndpoint))
            {
                var topicArn = EnsureTopic(message.Header.Topic, client);
                var publishRequest = new PublishRequest(topicArn, messageString);
                client.PublishAsync(publishRequest).Wait();
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
        /// Ensures the topic. The call to create topic is idempotent and just returns the arn if it already exists. Therefore there is 
        /// no nee to check then create if it does not exist, as this would be extral calls
        /// </summary>
        /// <param name="topicName">Name of the topic.</param>
        /// <param name="client">The client.</param>
        /// <returns>System.String.</returns>
        private string EnsureTopic(string topicName, AmazonSimpleNotificationServiceClient client)
        {
            _logger.Value.DebugFormat("Topic with name {0} does not exist. Creating new topic", topicName);
            var topicResult = client.CreateTopicAsync(new CreateTopicRequest(topicName)).Result;
            return topicResult.HttpStatusCode == HttpStatusCode.OK ? topicResult.TopicArn : string.Empty;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            
        }
    }
}
