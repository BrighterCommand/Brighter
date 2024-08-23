#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// Class SqsMessageProducer.
    /// </summary>
    public class SqsMessageProducer : AWSMessagingGateway, IAmAMessageProducerSync
    {
        private readonly AWSMessagingGatewayConnection _connection;
        private readonly SnsPublication _publication;
        private readonly AWSClientFactory _clientFactory;
        
        /// <summary>
        /// The publication configuration for this producer
        /// </summary>
        public Publication Publication { get { return _publication; } }

        /// <summary>
        /// The OTel Span we are writing Producer events too
        /// </summary>
        public Activity Span { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageProducer"/> class.
        /// </summary>
        /// <param name="connection">How do we connect to AWS in order to manage middleware</param>
        /// <param name="publication">Configuration of a producer</param>
        public SqsMessageProducer(AWSMessagingGatewayConnection connection, SnsPublication publication)
            : base(connection)
        {
            _connection = connection;
            _publication = publication;
            _clientFactory = new AWSClientFactory(connection);

            if (publication.TopicArn != null)
                ChannelTopicArn = publication.TopicArn;

        }
        
       public bool ConfirmTopicExists(string topic = null)
       {
           //Only do this on first send for a topic for efficiency; won't auto-recreate when goes missing at runtime as a result
           if (string.IsNullOrEmpty(ChannelTopicArn))
           {
               EnsureTopic(
                   topic != null ? new RoutingKey(topic) : _publication.Topic,
                   _publication.SnsAttributes,
                   _publication.FindTopicBy,
                   _publication.MakeChannels);
           }

           return !string.IsNullOrEmpty(ChannelTopicArn);
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

            using var client = _clientFactory.CreateSnsClient();
            var publisher = new SqsMessagePublisher(ChannelTopicArn, client, _publication.SnsSubjectGenerator);
            var messageId = publisher.Publish(message);
            if (messageId != null)
            {
                s_logger.LogDebug(
                    "SQSMessageProducer: Published message with topic {Topic}, Brighter messageId {MessageId} and SNS messageId {SNSMessageId}",
                    message.Header.Topic, message.Id, messageId);
                return;
            }

            throw new InvalidOperationException(
                string.Format($"Failed to publish message with topic {message.Header.Topic} and id {message.Id} and message: {message.Body}"));
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
