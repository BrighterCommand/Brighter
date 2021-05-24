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

using System.Collections.Generic;
using System.Linq;
using Amazon.SimpleNotificationService;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// Class SqsMessageProducer.
    /// </summary>
    public class SqsMessageProducer : AWSMessagingGateway, IAmAMessageProducer
    {
        public int MaxOutStandingMessages { get; set; } = -1;
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;

        private readonly AWSMessagingGatewayConnection _connection;
        private readonly SqsPublication _publication;
        private readonly Dictionary<string, string> _ensuredTopics = new Dictionary<string, string>();

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
            _publication = publication;

            //If the user has already established these by external creation, copy them
            if (_publication.TopicArns != null && _publication.TopicArns.Keys.Any())
            {
                foreach (var key in _publication.TopicArns.Keys)
                {
                    _ensuredTopics.Add(key, _publication.TopicArns[key]);
                }
            }

            MaxOutStandingMessages = publication.MaxOutStandingMessages;
            MaxOutStandingCheckIntervalMilliSeconds = publication.MaxOutStandingMessages;
        }
        
       public void ConfirmTopicExists(string topic)
       {
           //Only do this on first send for a topic for efficiency; won't auto-recreate when goes missing at runtime as a result
           if (!_ensuredTopics.ContainsKey(topic))
           {
               var topicArn = EnsureTopic(
                   new RoutingKey(topic),
                   _publication.SnsAttributes,
                   _publication.FindTopicBy,
                   _publication.MakeChannels);
               _ensuredTopics.Add(topic, topicArn);
           }
       }

       /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Send(Message message)
        {
            s_logger.LogDebug("SQSMessageProducer: Publishing message with topic {Topic} and id {Id} and message: {Request}", 
                message.Header.Topic, message.Id, message.Body);
            
            ConfirmTopicExists(message.Header.Topic);

            using (var client = new AmazonSimpleNotificationServiceClient(_connection.Credentials, _connection.Region))
            {
                var publisher = new SqsMessagePublisher(_ensuredTopics[message.Header.Topic], client);
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
