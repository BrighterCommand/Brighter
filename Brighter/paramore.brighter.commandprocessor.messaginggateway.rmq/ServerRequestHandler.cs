// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
// <copyright file="ServerRequestHandler.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Text;
using Common.Logging;
using paramore.brighter.serviceactivator;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using paramore.brighter.commandprocessor.extensions;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class ServerRequestHandler .
    /// The <see cref="ServerRequestHandler"/> is used on the server to receive messages from the broker. It abstracts away the details of 
    /// inter-process communication tasks from the server. It handles connection establishment, request reception and dispatching, 
    /// result sending, and error handling.
    /// </summary>
    public class ServerRequestHandler : MessageGateway, IAmAServerRequestHandler 
    {
        const bool AUTO_ACK = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public ServerRequestHandler(ILog logger):base(logger) {}

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
         {
            if (Channel != null)
            {
                Logger.Debug(m => m("RMQMessagingGateway: Acknowledging message {0} as completed", message.Id));
                Channel.BasicAck((ulong)message.Header.Bag["DeliveryTag"], false);
            }
         }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        public void Purge(string queueName)
        {
            if (Channel != null)
            {
                Logger.Debug(m => m("RMQMessagingGateway: Purging channel"));
                Channel.QueuePurge(queueName);
            }
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
        public void Reject(Message message, bool requeue)
        {
            if (Channel != null)
            {
                Logger.Debug(m => m("RMQMessagingGateway: NoAck message {0}", message.Id));
                Channel.BasicNack((ulong)message.Header.Bag["DeliveryTag"], false, requeue);
            }
        }


        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        public Message Receive(string queueName, int timeoutInMilliseconds)
        {

            Logger.Debug(m => m("RMQMessagingGateway: Preparing  to retrieve next message via exchange {0}", Configuration.Exchange.Name));

            if (!Connect(queueName))
            {
                Logger.Debug(m => m("RMQMessagingGateway: Unable to connect to the exchange {0}", Configuration.Exchange.Name));
                throw ConnectionFailure;
            }

            var message = new Message();
            try
            {
                var consumer = new QueueingBasicConsumer(Channel);
                Channel.BasicConsume(queueName, AUTO_ACK, consumer);
                BasicDeliverEventArgs fromQueue;
                consumer.Queue.Dequeue(timeoutInMilliseconds, out fromQueue);
                if (fromQueue != null)
                {
                    message = CreateMessage(fromQueue);
                    Logger.Debug(m => m("RMQMessagingGateway: Recieved message from exchange {0} on connection {1} with topic {2} and id {3} and body {4}", Configuration.Exchange.Name, Configuration.AMPQUri.Uri.ToString(), message.Header.Topic, message.Id, message.Body.Value));
                }
                else
                {
                    Logger.Debug(m => m("RMQMessagingGateway: Time out without recieving message from exchange {0} on connection {1} with topic {2}", Configuration.Exchange.Name, Configuration.AMPQUri.Uri.ToString(), queueName));
                }
            }
            catch (Exception e)
            {
                Logger.Error(m => m("RMQMessagingGateway: There was an error listening to channel {0} of {1}", queueName, e.ToString()));
                throw;
            }

            return message;

        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            CloseConnection();
        }


        private Message CreateMessage(BasicDeliverEventArgs fromQueue)
        {
            var messageId = fromQueue.BasicProperties.MessageId;
            var message = new Message(
                new MessageHeader(Guid.Parse(messageId), 
                    Encoding.UTF8.GetString((byte[])fromQueue.BasicProperties.Headers["Topic"]), 
                    GetMessageType(fromQueue)),
                new MessageBody(Encoding.UTF8.GetString(fromQueue.Body))
                );


            fromQueue.BasicProperties.Headers.Each((header) => message.Header.Bag.Add(header.Key, Encoding.UTF8.GetString((byte[])header.Value)));

            message.Header.Bag["DeliveryTag"] = fromQueue.DeliveryTag;
            return message;
        }


        private MessageType GetMessageType(BasicDeliverEventArgs fromQueue)
        {
            return (MessageType)Enum.Parse(typeof(MessageType),  Encoding.UTF8.GetString((byte[])fromQueue.BasicProperties.Headers["MessageType"]));
        }
    }
}
