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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// Class SqsMessageProducer.
    /// </summary>
    public class SqsMessageProducer : AWSMessagingGateway, IAmAMessageProducerSync, IAmAMessageProducerAsync
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
        public Activity? Span { get; set; }
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
        
       public async Task<bool> ConfirmTopicExistsAsync(string? topic = null)
       {
           //Only do this on first send for a topic for efficiency; won't auto-recreate when goes missing at runtime as a result
           if (!string.IsNullOrEmpty(ChannelTopicArn)) return !string.IsNullOrEmpty(ChannelTopicArn);
           
           RoutingKey? routingKey = null;
           if (topic is null && _publication.Topic is not null)
               routingKey = _publication.Topic;
           else if (topic is not null)
               routingKey = new RoutingKey(topic);
               
           if (routingKey is null)
               throw new ConfigurationException("No topic specified for producer");
               
           await EnsureTopicAsync(
               routingKey,
               _publication.FindTopicBy,
               _publication.SnsAttributes, _publication.MakeChannels);

           return !string.IsNullOrEmpty(ChannelTopicArn);
       }
       
       /// <summary>
       /// Sends the specified message.
       /// </summary>
       /// <param name="message">The message.</param>
       public async Task SendAsync(Message message)
       {
           s_logger.LogDebug("SQSMessageProducer: Publishing message with topic {Topic} and id {Id} and message: {Request}", 
               message.Header.Topic, message.Id, message.Body);
            
           await ConfirmTopicExistsAsync(message.Header.Topic);
           
           if (string.IsNullOrEmpty(ChannelTopicArn))
               throw new InvalidOperationException($"Failed to publish message with topic {message.Header.Topic} and id {message.Id} and message: {message.Body} as the topic does not exist");

           using var client = _clientFactory.CreateSnsClient();
           var publisher = new SqsMessagePublisher(ChannelTopicArn!, client);
           var messageId = await publisher.PublishAsync(message);
           
           if (messageId == null)
               throw new InvalidOperationException($"Failed to publish message with topic {message.Header.Topic} and id {message.Id} and message: {message.Body}");
           
           s_logger.LogDebug(
               "SQSMessageProducer: Published message with topic {Topic}, Brighter messageId {MessageId} and SNS messageId {SNSMessageId}",
               message.Header.Topic, message.Id, messageId);
       }

        /// <summary>
        /// Sends the specified message.
        /// Sync over Async
        /// </summary>
        /// <param name="message">The message.</param>
        public void Send(Message message)
        {
            SendAsync(message).Wait();
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delay">The sending delay</param>
        /// <returns>Task.</returns>
        public void SendWithDelay(Message message, TimeSpan? delay= null)
        {
            //TODO: Delay should set a visibility timeout
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
