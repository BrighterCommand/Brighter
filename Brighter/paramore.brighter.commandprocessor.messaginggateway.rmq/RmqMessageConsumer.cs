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
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;
using Polly.CircuitBreaker;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

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
        readonly string queueName;
        readonly string routingKey;
        const bool AUTO_ACK = false;
        /// <summary>
        /// The consumer
        /// </summary>
        QueueingBasicConsumer consumer;
        readonly RmqMessageCreator messageCreator;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RmqMessageConsumer(string queueName, string routingKey, ILog logger): base(logger)
        {
            this.queueName = queueName;
            this.routingKey = routingKey;
            messageCreator = new RmqMessageCreator(logger);
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            var deliveryTag = message.GetDeliveryTag();
            try
            {
                EnsureChannel(queueName);
                Logger.InfoFormat("RmqMessageConsumer: Acknowledging message {0} as completed with delivery tag {1}", message.Id, deliveryTag);
                Channel.BasicAck(deliveryTag, false);
            }
            catch (Exception exception) 
            {
                Logger.ErrorException("RmqMessageConsumer: Error acknowledging message {0} as completed with delivery tag {1}", exception, message.Id, deliveryTag);
                throw;
            }
        }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public void Purge()
        {
            try
            {
                EnsureChannel(queueName);
                Logger.DebugFormat("RmqMessageConsumer: Purging channel {0}", queueName);

                try { Channel.QueuePurge(queueName); }
                catch (OperationInterruptedException operationInterruptedException) {
                    if (operationInterruptedException.ShutdownReason.ReplyCode == 404) { return; }
                    throw;
                }
            }
            catch (Exception exception)
            {
                Logger.ErrorException("RmqMessageConsumer: Error purging channel {0}", exception, queueName);
                throw;
            }
        }

        public void Requeue(Message message)
        {
            try
            {
                EnsureChannel(queueName);
                var rmqMessagePublisher = new RmqMessagePublisher(Channel, Configuration.Exchange.Name);
                Logger.DebugFormat("RmqMessageConsumer: Re-queueing message {0}", message.Id);
                rmqMessagePublisher.PublishMessage(message);
                Reject(message, false);
            }
            catch (Exception exception) 
            {
                Logger.ErrorException("RmqMessageConsumer: Error re-queueing message {0}", exception, message.Id);
                throw;
            }
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
        public void Reject(Message message, bool requeue)
        {
            try
            {
                EnsureChannel(queueName);
                Logger.DebugFormat("RmqMessageConsumer: NoAck message {0}", message.Id);
                Channel.BasicNack(message.GetDeliveryTag(), false, requeue);
            }
            catch (Exception exception) 
            {
                Logger.ErrorException("RmqMessageConsumer: Error try to NoAck message {0}", exception, message.Id);
                throw;
            }
        }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        public Message Receive(int timeoutInMilliseconds)
        {
            Logger.DebugFormat("RmqMessageConsumer: Preparing to retrieve next message from queue {0} with routing key {1} via exchange {2} on connection {3}", queueName, routingKey, Configuration.Exchange.Name, Configuration.AMPQUri.GetSantizedUri());

            var message = new Message();
            try
            {
                EnsureConsumer();
                BasicDeliverEventArgs fromQueue;
                if (consumer.Queue.Dequeue(timeoutInMilliseconds, out fromQueue))
                {
                    message = messageCreator.CreateMessage(fromQueue);
                    Logger.InfoFormat(
                        "RmqMessageConsumer: Received message from queue {0} with routing key {1} via exchange {2} on connection {3}, message: {5}{4}",
                        queueName, routingKey, Configuration.Exchange.Name, Configuration.AMPQUri.GetSantizedUri(), JsonConvert.SerializeObject(message),
                        Environment.NewLine);
                }
                else
                {
                    Logger.DebugFormat(
                        "RmqMessageConsumer: Time out without receiving message from queue {0} with routing key {1} via exchange {2} on connection {3}",
                        queueName, routingKey, Configuration.Exchange.Name, Configuration.AMPQUri.GetSantizedUri());
                }
            }
            catch (EndOfStreamException endOfStreamException)
            {
                Logger.DebugException(
                    "RmqMessageConsumer: The consumer {4} was canceled, the model closed, or the connection went away. Listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                    endOfStreamException,
                    queueName,
                    routingKey,
                    Configuration.Exchange.Name,
                    Configuration.AMPQUri.GetSantizedUri(),
                    consumer.ConsumerTag);
                consumer = null;
            }
            catch (BrokerUnreachableException bue)
            {
                Logger.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                                      bue,
                                      queueName,
                                      routingKey,
                                      Configuration.Exchange.Name,
                                      Configuration.AMPQUri.GetSantizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", bue);
            }
            catch (AlreadyClosedException ace)
            {
                Logger.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                                      ace,
                                      queueName,
                                      routingKey,
                                      Configuration.Exchange.Name,
                                      Configuration.AMPQUri.GetSantizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", ace);
            }
            catch (OperationInterruptedException oie)
            {
                Logger.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                                      oie,
                                      queueName,
                                      routingKey,
                                      Configuration.Exchange.Name,
                                      Configuration.AMPQUri.GetSantizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", oie);
            }
            catch (NotSupportedException nse)
            {
                Logger.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                                      nse,
                                      queueName,
                                      routingKey,
                                      Configuration.Exchange.Name,
                                      Configuration.AMPQUri.GetSantizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", nse);
                
            }
            catch (BrokenCircuitException bce)
            {
                Logger.WarnFormat("CIRCUIT BROKEN: RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}", 
                    queueName, 
                    routingKey, 
                    Configuration.Exchange.Name, 
                    Configuration.AMPQUri.GetSantizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", bce);
            }
            catch (Exception exception)
            {
                Logger.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}", exception, queueName, routingKey, Configuration.Exchange.Name, Configuration.AMPQUri.GetSantizedUri());
                throw;
            }

            return message;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            CancelConsumer();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~RmqMessageConsumer()
        {
            Dispose(false);
        }

        protected virtual void CreateConsumer()
        {
            consumer = new QueueingBasicConsumer(Channel);
            Channel.BasicConsume(queueName, AUTO_ACK, consumer);

            Logger.InfoFormat("RmqMessageConsumer: Created consumer with ConsumerTag {4} for queue {0} with routing key {1} via exchange {2} on connection {3}",
                              queueName,
                              routingKey,
                              Configuration.Exchange.Name,
                              Configuration.AMPQUri.GetSantizedUri(),
                              consumer.ConsumerTag);
        }

        void EnsureConsumer()
        {
            if (consumer == null || !consumer.IsRunning)
            {
                EnsureChannelBind();
                CreateConsumer();
            }
        }

        void EnsureChannelBind()
        {
            EnsureChannel(queueName);
            Logger.DebugFormat("RMQMessagingGateway: Creating queue {0} on connection {1}", queueName, Configuration.AMPQUri.GetSantizedUri());
            Channel.QueueDeclare(queueName, false, false, false, SetQueueArguments());
            Channel.QueueBind(queueName, Configuration.Exchange.Name, routingKey);
        }

        Dictionary<string, object> SetQueueArguments()
        {
            var arguments = new Dictionary<string, object>();
            QueueIsMirroredAcrossAllNodesInTheCluster(arguments);
            return arguments;
        }

        void QueueIsMirroredAcrossAllNodesInTheCluster(Dictionary<string, object> arguments)
        {
            if (Configuration.Queues.HighAvailability) { arguments.Add("x-ha-policy", "all"); }
        }

        void CancelConsumer()
        {
            if (consumer != null )
            {
                if (consumer.IsRunning)
                {
                    consumer.OnCancel();
                    Channel.BasicCancel(consumer.ConsumerTag);
                }

                Logger.InfoFormat("RmqMessageConsumer: Cancelled consumer with ConsumerTag {0}", consumer.ConsumerTag);
                consumer = null;
            }
        }
    }
}
