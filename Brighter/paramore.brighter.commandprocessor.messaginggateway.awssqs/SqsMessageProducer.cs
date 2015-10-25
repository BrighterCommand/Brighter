// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.awssqs
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
using System.Net;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    /// <summary>
    /// Class SqsMessageProducer.
    /// </summary>
    public class SqsMessageProducer : IAmAMessageProducer
    {
        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILog _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageProducer"/> class.
        /// </summary>
        public SqsMessageProducer() 
            : this(LogProvider.GetCurrentClassLogger())
        {}


        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageProducer"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public SqsMessageProducer(ILog logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Send(Message message)
        {
            var messageString = JsonConvert.SerializeObject(message);
            _logger.DebugFormat("SQSMessageProducer: Publishing message with topic {0} and id {1} and message: {2}", message.Header.Topic, message.Id, messageString);
            
            using (var client = new AmazonSimpleNotificationServiceClient())
            {
                var topicArn = EnsureTopic(message.Header.Topic, client);

                var publishRequest = new PublishRequest(topicArn, messageString);
                client.Publish(publishRequest);
            }
        }

        /// <summary>
        /// Ensures the topic.
        /// </summary>
        /// <param name="topicName">Name of the topic.</param>
        /// <param name="client">The client.</param>
        /// <returns>System.String.</returns>
        private string EnsureTopic(string topicName, AmazonSimpleNotificationServiceClient client)
        {
            var topic = client.FindTopic(topicName);
            if (topic != null)
                return topic.TopicArn;

            _logger.DebugFormat("Topic with name {0} does not exist. Creating new topic", topicName);
            var topicResult = client.CreateTopic(topicName);
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
