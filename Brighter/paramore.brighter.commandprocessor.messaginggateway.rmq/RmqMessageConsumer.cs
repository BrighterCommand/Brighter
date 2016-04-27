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
    /// Class RmqMessageConsumer.
    /// The <see cref="RmqMessageConsumer"/> is used on the server to receive messages from the broker. It abstracts away the details of 
    /// inter-process communication tasks from the server. It handles connection establishment, request reception and dispatching, 
    /// result sending, and error handling.
    /// </summary>
    public class RmqMessageConsumer : MessageGateway, IAmAMessageConsumerSupportingDelay
    {
        private readonly string _queueName;
        private readonly string _routingKey;
        private readonly bool _isDurable;
        private const bool AutoAck = false;
        /// <summary>
        /// The consumer
        /// </summary>
        private QueueingBasicConsumer _consumer;
        private readonly RmqMessageCreator _messageCreator;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// </summary>
        /// <param name="queueName">The queue name.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="isDurable"></param>
        /// <param name="logger">The logger.</param>
        public RmqMessageConsumer(string queueName, string routingKey, bool isDurable, string connectionName = "") 
            : this(queueName, routingKey, isDurable, LogProvider.GetCurrentClassLogger(), connectionName) {}


        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// Use this if you need to override the logger in tests.
        /// </summary>
        /// <param name="queueName">The queue name.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="isDurable"></param>
        /// <param name="logger">The logger.</param>
        public RmqMessageConsumer(string queueName, string routingKey, bool isDurable, ILog logger, string connectionName = "") : base(logger, connectionName)
        {
            _queueName = queueName;
            _routingKey = routingKey;
            _isDurable = isDurable;
            _messageCreator = new RmqMessageCreator(logger);
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
                EnsureChannel(_queueName);
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
                EnsureChannel(_queueName);
                Logger.DebugFormat("RmqMessageConsumer: Purging channel {0}", _queueName);

                try { Channel.QueuePurge(_queueName); }
                catch (OperationInterruptedException operationInterruptedException)
                {
                    if (operationInterruptedException.ShutdownReason.ReplyCode == 404) { return; }
                    throw;
                }
            }
            catch (Exception exception)
            {
                Logger.ErrorException("RmqMessageConsumer: Error purging channel {0}", exception, _queueName);
                throw;
            }
        }

        public void Requeue(Message message)
        {
            this.Requeue(message, 0);
        }

        public void Requeue(Message message, int delayMilliseconds)
        {
            try
            {
                Logger.DebugFormat("RmqMessageConsumer: Re-queueing message {0} with a delay of {1} milliseconds", message.Id, delayMilliseconds);
                EnsureChannel(_queueName);
                var rmqMessagePublisher = new RmqMessagePublisher(Channel, Configuration.Exchange.Name, Logger);
                rmqMessagePublisher.RequeueMessage(message, _queueName, delayMilliseconds);
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
                EnsureChannel(_queueName);
                Logger.InfoFormat("RmqMessageConsumer: NoAck message {0} with delivery tag {1}", message.Id, message.GetDeliveryTag());
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
            Logger.DebugFormat("RmqMessageConsumer: Preparing to retrieve next message from queue {0} with routing key {1} via exchange {2} on connection {3}", _queueName, _routingKey, Configuration.Exchange.Name, Configuration.AMPQUri.GetSanitizedUri());

            var message = new Message();
            try
            {
                EnsureConsumer();
                BasicDeliverEventArgs fromQueue;
                if (_consumer.Queue.Dequeue(timeoutInMilliseconds, out fromQueue))
                {
                    message = _messageCreator.CreateMessage(fromQueue);
                    Logger.InfoFormat(
                        "RmqMessageConsumer: Received message from queue {0} with routing key {1} via exchange {2} on connection {3}, message: {5}{4}",
                        _queueName, _routingKey, Configuration.Exchange.Name, Configuration.AMPQUri.GetSanitizedUri(), JsonConvert.SerializeObject(message),
                        Environment.NewLine);
                }
                else
                {
                    Logger.DebugFormat(
                        "RmqMessageConsumer: Time out without receiving message from queue {0} with routing key {1} via exchange {2} on connection {3}",
                        _queueName, _routingKey, Configuration.Exchange.Name, Configuration.AMPQUri.GetSanitizedUri());
                }
            }
            catch (EndOfStreamException endOfStreamException)
            {
                Logger.DebugException(
                    "RmqMessageConsumer: The consumer {4} was canceled, the model closed, or the connection went away. Listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                    endOfStreamException,
                    _queueName,
                    _routingKey,
                    Configuration.Exchange.Name,
                    Configuration.AMPQUri.GetSanitizedUri(),
                    _consumer.ConsumerTag);
            }
            catch (BrokerUnreachableException bue)
            {
                Logger.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                                      bue,
                                      _queueName,
                                      _routingKey,
                                      Configuration.Exchange.Name,
                                      Configuration.AMPQUri.GetSanitizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", bue);
            }
            catch (AlreadyClosedException ace)
            {
                Logger.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                                      ace,
                                      _queueName,
                                      _routingKey,
                                      Configuration.Exchange.Name,
                                      Configuration.AMPQUri.GetSanitizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", ace);
            }
            catch (OperationInterruptedException oie)
            {
                Logger.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                                      oie,
                                      _queueName,
                                      _routingKey,
                                      Configuration.Exchange.Name,
                                      Configuration.AMPQUri.GetSanitizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", oie);
            }
            catch (NotSupportedException nse)
            {
                Logger.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                                      nse,
                                      _queueName,
                                      _routingKey,
                                      Configuration.Exchange.Name,
                                      Configuration.AMPQUri.GetSanitizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", nse);
            }
            catch (BrokenCircuitException bce)
            {
                Logger.WarnFormat("CIRCUIT BROKEN: RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                    _queueName,
                    _routingKey,
                    Configuration.Exchange.Name,
                    Configuration.AMPQUri.GetSanitizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", bce);
            }
            catch (Exception exception)
            {
                Logger.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}", exception, _queueName, _routingKey, Configuration.Exchange.Name, Configuration.AMPQUri.GetSanitizedUri());
                throw;
            }

            return message;
        }

        protected virtual void CreateConsumer()
        {
            _consumer = new QueueingBasicConsumer(Channel);

            Channel.BasicConsume(_queueName, AutoAck, string.Empty, SetConsumerArguments(), _consumer);

            _consumer.HandleBasicConsumeOk(string.Empty);
            
            Logger.InfoFormat("RmqMessageConsumer: Created consumer with ConsumerTag {4} for queue {0} with routing key {1} via exchange {2} on connection {3}",
                              _queueName,
                              _routingKey,
                              Configuration.Exchange.Name,
                              Configuration.AMPQUri.GetSanitizedUri(),
                              _consumer.ConsumerTag);
        }

        private void EnsureConsumer()
        {
            if (_consumer == null || !_consumer.IsRunning)
            {
                EnsureChannelBind();
                CreateConsumer();
            }
        }

        private void EnsureChannelBind()
        {
            EnsureChannel(_queueName);

            Logger.DebugFormat("RMQMessagingGateway: Creating queue {0} on connection {1}", _queueName, Configuration.AMPQUri.GetSanitizedUri());

            Channel.QueueDeclare(_queueName, _isDurable, false, false, SetQueueArguments());

            Channel.QueueBind(_queueName, Configuration.Exchange.Name, _routingKey);
        }

        private Dictionary<string, object> SetConsumerArguments()
        {
            var arguments = new Dictionary<string, object>();
            if (IsQueueMirroredAcrossAllNodesInTheCluster)
            {
                arguments.Add("x-cancel-on-ha-failover", true);
            }
            return arguments;
        }

        private Dictionary<string, object> SetQueueArguments()
        {
            var arguments = new Dictionary<string, object>();
            if (IsQueueMirroredAcrossAllNodesInTheCluster)
            {
                // Only work for RabbitMQ Server version before 3.0
                //http://www.rabbitmq.com/blog/2012/11/19/breaking-things-with-rabbitmq-3-0/
                arguments.Add("x-ha-policy", "all");
            } 
            return arguments;
        }

        private bool IsQueueMirroredAcrossAllNodesInTheCluster{ get { return Configuration.Queues.HighAvailability; } }

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

        private void CancelConsumer()
        {
            if (_consumer != null)
            {
                if (_consumer.IsRunning)
                {
                    _consumer.OnCancel();
                }

                _consumer = null;
            }
        }
    }
}
