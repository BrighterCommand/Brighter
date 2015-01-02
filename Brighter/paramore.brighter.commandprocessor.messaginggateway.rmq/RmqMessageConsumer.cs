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
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using paramore.brighter.commandprocessor.extensions;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class ServerRequestHandler .
    /// The <see cref="RmqMessageConsumer"/> is used on the server to receive messages from the broker. It abstracts away the details of 
    /// inter-process communication tasks from the server. It handles connection establishment, request reception and dispatching, 
    /// result sending, and error handling.
    /// </summary>
    public class RmqMessageConsumer : MessageGateway, IAmAMessageConsumer 
    {
        const bool AUTO_ACK = false;
        /// <summary>
        /// The consumer
        /// </summary>
        QueueingBasicConsumer consumer;
        private readonly RmqMessageCreator messageCreator;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RmqMessageConsumer(ILog logger) : base(logger)
        {
            messageCreator = new RmqMessageCreator(logger);
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
         {
            if (Channel != null)
            {
                var deliveryTag = (ulong)message.Header.Bag["DeliveryTag"];
                Logger.Debug(m =>m("RmqMessageConsumer: Acknowledging message {0} as completed with delivery tag {1}",message.Id, deliveryTag));
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
                Logger.Debug(m => m("RmqMessageConsumer: Purging channel"));
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
                Logger.Debug(m => m("RmqMessageConsumer: NoAck message {0}", message.Id));
                Channel.BasicNack((ulong)message.Header.Bag["DeliveryTag"], false, requeue);
            }
        }


        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        public Message Receive(string queueName, string routingKey, int timeoutInMilliseconds)
        {
            Logger.Debug(m => m("RmqMessageConsumer: Preparing  to retrieve next message via exchange {0}", Configuration.Exchange.Name));

            if (!Connect(queueName, routingKey, true))
            {
                Logger.Debug(m => m("RmqMessageConsumer: Unable to connect to the exchange {0}", Configuration.Exchange.Name));
                throw ConnectionFailure;
            }

            var message = new Message();
            try
            {
                BasicDeliverEventArgs fromQueue;
                if (consumer.Queue.Dequeue(timeoutInMilliseconds, out fromQueue))
                {
                    message = messageCreator.CreateMessage(fromQueue);
                    var deliveryTag = (ulong)message.Header.Bag["DeliveryTag"];
                    Logger.Debug(m => m("RmqMessageConsumer: Recieved message with delivery tag {5} from exchange {0} on connection {1} with topic {2} and id {3} and body {4}", 
                        Configuration.Exchange.Name, Configuration.AMPQUri.Uri.ToString(), message.Header.Topic, message.Id, message.Body.Value, deliveryTag));
                }
                else
                {
                    Logger.Debug(m => m("RmqMessageConsumer: Time out without recieving message from exchange {0} on connection {1} with topic {2}", Configuration.Exchange.Name, Configuration.AMPQUri.Uri.ToString(), queueName));
                }
            }
            catch (Exception e)
            {
                Logger.Error(m => m("RmqMessageConsumer: There was an error listening to channel {0} of {1}", queueName, e.ToString()));
                throw;
            }

            return message;

        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~RmqMessageConsumer()
        {
            Dispose(false);
        }

        void Dispose(bool disposing)
        {
            CloseConnection();
        }

        /// <summary>
        /// Connects the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey"></param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        protected override bool Connect(string queueName = "", string routingKey = "", bool createQueues = false)
        {
            if (NotConnected())
            {
                if (base.Connect(queueName, routingKey, createQueues))
                {
                    consumer = new QueueingBasicConsumer(Channel);
                    Channel.BasicConsume(queueName, AUTO_ACK, consumer);

                    return true;
                }

                return false;
            }

            return true;
        }
    }
}
