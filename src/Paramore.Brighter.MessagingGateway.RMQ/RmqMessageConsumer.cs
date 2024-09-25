﻿#region Licence

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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Polly.CircuitBreaker;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Class RmqMessageConsumer.
    /// The <see cref="RmqMessageConsumer"/> is used on the server to receive messages from the broker. It abstracts away the details of 
    /// inter-process communication tasks from the server. It handles subscription establishment, request reception and dispatching, 
    /// result sending, and error handling.
    /// </summary>
    public class RmqMessageConsumer : RmqMessageGateway, IAmAMessageConsumer
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageConsumer>();

        private PullConsumer _consumer;
        private readonly ChannelName _queueName;
        private readonly RoutingKeys _routingKeys;
        private readonly bool _isDurable;
        private readonly RmqMessageCreator _messageCreator;
        private readonly Message _noopMessage = new Message();
        private readonly string _consumerTag;
        private readonly OnMissingChannel _makeChannels;
        private readonly ushort _batchSize;
        private readonly bool _highAvailability;
        private readonly ChannelName _deadLetterQueueName;
        private readonly RoutingKey _deadLetterRoutingKey;
        private readonly bool _hasDlq;
        private readonly TimeSpan? _ttl;
        private readonly int? _maxQueueLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageGateway" /> class.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="queueName">The queue name.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="isDurable">Is the queue definition persisted</param>
        /// <param name="highAvailability">Is the queue available on all nodes in a cluster</param>
        /// <param name="batchSize">How many messages to retrieve at one time; ought to be size of channel buffer</param>
        /// <param name="deadLetterQueueName">The dead letter queue</param>
        /// <param name="deadLetterRoutingKey">The routing key for dead letter messages</param>
        /// <param name="ttl">How long before a message on the queue expires. Defaults to infinite</param>
        /// <param name="maxQueueLength">How lare can the buffer grow before we stop accepting new work?</param>
        /// <param name="makeChannels">Should we validate, or create missing channels</param>
        public RmqMessageConsumer(
            RmqMessagingGatewayConnection connection,
            ChannelName queueName,
            RoutingKey routingKey,
            bool isDurable,
            bool highAvailability = false,
            int batchSize = 1,
            ChannelName deadLetterQueueName = null,
            RoutingKey deadLetterRoutingKey = null,
            TimeSpan? ttl = null,
            int? maxQueueLength = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create)
            : this(connection, queueName, new RoutingKeys([routingKey]), isDurable, highAvailability,
                batchSize, deadLetterQueueName, deadLetterRoutingKey, ttl, maxQueueLength, makeChannels)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessageGateway" /> class.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="queueName">The queue name.</param>
        /// <param name="routingKeys">The routing keys.</param>
        /// <param name="isDurable">Is the queue persisted to disk</param>
        /// <param name="highAvailability">Are the queues mirrored across nodes of the cluster</param>
        /// <param name="batchSize">How many messages to retrieve at one time; ought to be size of channel buffer</param>
        /// <param name="deadLetterQueueName">The dead letter queue</param>
        /// <param name="deadLetterRoutingKey">The routing key for dead letter messages</param>
        /// <param name="ttl">How long before a message on the queue expires. Defaults to infinite</param>
        /// <param name="maxQueueLength">The maximum number of messages on the queue before we begin to reject publication of messages</param>
        /// <param name="makeChannels">Should we validate or create missing channels</param>
        public RmqMessageConsumer(
            RmqMessagingGatewayConnection connection,
            ChannelName queueName,
            RoutingKeys routingKeys,
            bool isDurable,
            bool highAvailability = false,
            int batchSize = 1,
            ChannelName deadLetterQueueName = null,
            RoutingKey deadLetterRoutingKey = null,
            TimeSpan? ttl = null,
            int? maxQueueLength = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create)
            : base(connection)
        {
            _queueName = queueName;
            _routingKeys = routingKeys;
            _isDurable = isDurable;
            _highAvailability = highAvailability;
            _messageCreator = new RmqMessageCreator();
            _batchSize = Convert.ToUInt16(batchSize);
            _makeChannels = makeChannels;
            _consumerTag = Connection.Name + Guid.NewGuid();
            _deadLetterQueueName = deadLetterQueueName;
            _deadLetterRoutingKey = deadLetterRoutingKey;
            _hasDlq = !string.IsNullOrEmpty(deadLetterQueueName) && !string.IsNullOrEmpty(_deadLetterRoutingKey);
            _ttl = ttl;
            _maxQueueLength = maxQueueLength;
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            var deliveryTag = message.DeliveryTag;
            try
            {
                EnsureBroker();
                s_logger.LogInformation(
                    "RmqMessageConsumer: Acknowledging message {Id} as completed with delivery tag {DeliveryTag}",
                    message.Id, deliveryTag);
                Channel.BasicAck(deliveryTag, false);
            }
            catch (Exception exception)
            {
                s_logger.LogError(exception,
                    "RmqMessageConsumer: Error acknowledging message {Id} as completed with delivery tag {DeliveryTag}",
                    message.Id, deliveryTag);
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
                //Why bind a queue? Because we use purge to initialize a queue for RPC
                EnsureChannel();
                s_logger.LogDebug("RmqMessageConsumer: Purging channel {ChannelName}", _queueName.Value);

                try { Channel.QueuePurge(_queueName.Value); }
                catch (OperationInterruptedException operationInterruptedException)
                {
                    if (operationInterruptedException.ShutdownReason.ReplyCode == 404) { return; }

                    throw;
                }
            }
            catch (Exception exception)
            {
                s_logger.LogError(exception, "RmqMessageConsumer: Error purging channel {ChannelName}",
                    _queueName.Value);
                throw;
            }
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="timeout">Time to delay delivery of the message.</param>
        /// <returns>True if message deleted, false otherwise</returns>
        public bool Requeue(Message message, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.Zero;

            try
            {
                s_logger.LogDebug("RmqMessageConsumer: Re-queueing message {Id} with a delay of {Delay} milliseconds",
                    message.Id, timeout.Value.Milliseconds);
                EnsureBroker(_queueName);

                var rmqMessagePublisher = new RmqMessagePublisher(Channel, Connection);
                if (DelaySupported)
                {
                    rmqMessagePublisher.RequeueMessage(message, _queueName, timeout.Value);
                }
                else
                {
                    if (timeout > TimeSpan.Zero) Task.Delay(timeout.Value).Wait();
                    rmqMessagePublisher.RequeueMessage(message, _queueName, TimeSpan.Zero);
                }

                //ack the original message to remove it from the queue
                var deliveryTag = message.DeliveryTag;
                s_logger.LogInformation(
                    "RmqMessageConsumer: Deleting message {Id} with delivery tag {DeliveryTag} as re-queued",
                    message.Id, deliveryTag);
                Channel.BasicAck(deliveryTag, false);

                return true;
            }
            catch (Exception exception)
            {
                s_logger.LogError(exception, "RmqMessageConsumer: Error re-queueing message {Id}", message.Id);
                return false;
            }
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Reject(Message message)
        {
            try
            {
                EnsureBroker(_queueName);
                s_logger.LogInformation("RmqMessageConsumer: NoAck message {Id} with delivery tag {DeliveryTag}",
                    message.Id, message.DeliveryTag);
                //if we have a DLQ, this will force over to the DLQ
                Channel.BasicReject(message.DeliveryTag, false);
            }
            catch (Exception exception)
            {
                s_logger.LogError(exception, "RmqMessageConsumer: Error try to NoAck message {Id}", message.Id);
                throw;
            }
        }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="timeOut">The timeout in milliseconds. We retry on timeout 5 ms intervals, with a min of 5ms
        /// until the timeout value is reached. </param>
        /// <returns>Message.</returns>
        public Message[] Receive(TimeSpan? timeOut = null)
        {
            s_logger.LogDebug(
                "RmqMessageConsumer: Preparing to retrieve next message from queue {ChannelName} with routing key {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );

            timeOut ??= TimeSpan.FromMilliseconds(5);

            try
            {
                EnsureChannel();

                var (resultCount, results) = _consumer.DeQueue(timeOut.Value, _batchSize);

                if (results != null && results.Length != 0)
                {
                    var messages = new Message[resultCount];
                    for (var i = 0; i < resultCount; i++)
                    {
                        var message = _messageCreator.CreateMessage(results[i]);
                        messages[i] = message;

                        s_logger.LogInformation(
                            "RmqMessageConsumer: Received message from queue {ChannelName} with routing key {RoutingKeys} via exchange {ExchangeName} on subscription {URL}, message: {Request}",
                            _queueName.Value,
                            string.Join(";", _routingKeys.Select(rk => rk.Value)),
                            Connection.Exchange.Name,
                            Connection.AmpqUri.GetSanitizedUri(),
                            JsonSerializer.Serialize(message, JsonSerialisationOptions.Options)
                        );
                    }

                    return messages;
                }
                else
                {
                    return new Message[] { _noopMessage };
                }
            }
            catch (EndOfStreamException endOfStreamException)
            {
                HandleEndOfStreamException(endOfStreamException);
            }
            catch (BrokerUnreachableException bue)
            {
                HandleBrokerUnreachableException(bue);
            }
            catch (AlreadyClosedException ace)
            {
                HandleAlreadyClosedException(ace);
            }
            catch (OperationInterruptedException oie)
            {
                HandleOperationInterruptedException(oie);
            }
            catch (TimeoutException te)
            {
                HandleTimeoutException(te);
            }
            catch (NotSupportedException nse)
            {
                HandleNotSupportedException(nse);
            }
            catch (BrokenCircuitException bce)
            {
                HandleBrokenCircuitException(bce);
            }
            catch (Exception exception)
            {
                HandleGeneralException(exception);
            }

            return new Message[] { _noopMessage }; // Default return in case of exception
        }

        protected virtual void EnsureChannel()
        {
            if (Channel == null || Channel.IsClosed)
            {
                EnsureBroker(_queueName);

                if (_makeChannels == OnMissingChannel.Create)
                {
                    CreateQueue();
                    BindQueue();
                }
                else if (_makeChannels == OnMissingChannel.Validate)
                {
                    ValidateQueue();
                }
                else if (_makeChannels == OnMissingChannel.Assume)
                {
                    ; //-- pass, here for clarity on fall through to use of queue directly on assume
                }

                CreateConsumer();

                s_logger.LogInformation(
                    "RmqMessageConsumer: Created rabbitmq channel {ConsumerNumber} for queue {ChannelName} with routing key/s {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                    Channel?.ChannelNumber,
                    _queueName.Value,
                    string.Join(";", _routingKeys.Select(rk => rk.Value)),
                    Connection.Exchange.Name,
                    Connection.AmpqUri.GetSanitizedUri()
                );
            }
        }

        private void CancelConsumer()
        {
            if (_consumer != null)
            {
                if (_consumer.IsRunning)
                {
                    Channel.BasicCancel(_consumerTag);
                }

                _consumer = null;
            }
        }

        private void CreateConsumer()
        {
            _consumer = new PullConsumer(Channel, _batchSize);

            Channel.BasicConsume(_queueName.Value, false, _consumerTag, SetQueueArguments(), _consumer);

            _consumer.HandleBasicConsumeOk(_consumerTag);

            s_logger.LogInformation(
                "RmqMessageConsumer: Created consumer for queue {ChannelName} with routing key {Topic} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
        }

        private void CreateQueue()
        {
            s_logger.LogDebug("RmqMessageConsumer: Creating queue {ChannelName} on subscription {URL}",
                _queueName.Value, Connection.AmpqUri.GetSanitizedUri());
            Channel.QueueDeclare(_queueName.Value, _isDurable, false, false, SetQueueArguments());
            if (_hasDlq) Channel.QueueDeclare(_deadLetterQueueName.Value, _isDurable, false, false);
        }

        private void BindQueue()
        {
            foreach (var key in _routingKeys)
            {
                Channel.QueueBind(_queueName.Value, Connection.Exchange.Name, key);
            }

            if (_hasDlq)
                Channel.QueueBind(_deadLetterQueueName.Value, GetDeadletterExchangeName(), _deadLetterRoutingKey.Value);
        }

        private void HandleEndOfStreamException(EndOfStreamException endOfStreamException)
        {
            s_logger.LogDebug(endOfStreamException,
                "RmqMessageConsumer: The model closed, or the subscription went away. Listening to queue {ChannelName} via exchange {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
            throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details",
                endOfStreamException);
        }

        private void HandleBrokerUnreachableException(BrokerUnreachableException bue)
        {
            s_logger.LogError(bue,
                "RmqMessageConsumer: There broker was unreachable listening to queue {ChannelName} via exchange {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
            ResetConnectionToBroker();
            throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", bue);
        }

        private void HandleAlreadyClosedException(AlreadyClosedException ace)
        {
            s_logger.LogError(ace,
                "RmqMessageConsumer: There subscription was already closed when listening to queue {ChannelName} via exchange {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
            ResetConnectionToBroker();
            throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", ace);
        }

        private void HandleOperationInterruptedException(OperationInterruptedException oie)
        {
            s_logger.LogError(oie,
                "RmqMessageConsumer: There was an error listening to queue {ChannelName} via exchange {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
            throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", oie);
        }

        private void HandleTimeoutException(TimeoutException te)
        {
            s_logger.LogError(te,
                "RmqMessageConsumer: The socket timed out whilst listening to queue {ChannelName} via exchange {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
            ResetConnectionToBroker();
            throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", te);
        }

        private void HandleNotSupportedException(NotSupportedException nse)
        {
            s_logger.LogError(nse,
                "RmqMessageConsumer: There was an error listening to queue {ChannelName} via exchange {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
            throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", nse);
        }

        private void HandleBrokenCircuitException(BrokenCircuitException bce)
        {
            s_logger.LogWarning(bce,
                "CIRCUIT BROKEN: RmqMessageConsumer: There was an error listening to queue {ChannelName} via exchange {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
            throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", bce);
        }

        private void HandleGeneralException(Exception exception)
        {
            s_logger.LogError(exception,
                "RmqMessageConsumer: There was an error listening to queue {ChannelName} via exchange {RoutingKeys} via exchange {ExchangeName} on subscription {URL}",
                _queueName.Value,
                string.Join(";", _routingKeys.Select(rk => rk.Value)),
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
            );
            throw exception;
        }


        private void ValidateQueue()
        {
            s_logger.LogDebug("RmqMessageConsumer: Validating queue {ChannelName} on subscription {URL}",
                _queueName.Value, Connection.AmpqUri.GetSanitizedUri());

            try
            {
                Channel.QueueDeclarePassive(_queueName.Value);
            }
            catch (Exception e)
            {
                throw new BrokerUnreachableException(e);
            }
        }

        private Dictionary<string, object> SetQueueArguments()
        {
            var arguments = new Dictionary<string, object>();
            if (_highAvailability)
            {
                // Only work for RabbitMQ Server version before 3.0
                //http://www.rabbitmq.com/blog/2012/11/19/breaking-things-with-rabbitmq-3-0/
                arguments.Add("x-ha-policy", "all");
            }

            if (_hasDlq)
            {
                //You can set a different exchange for the DLQ to the Queue
                arguments.Add("x-dead-letter-exchange", GetDeadletterExchangeName());
                arguments.Add("x-dead-letter-routing-key", _deadLetterRoutingKey.Value);
            }

            if (_ttl.HasValue)
            {
                arguments.Add("x-message-ttl", _ttl.Value.Milliseconds);
            }

            if (_maxQueueLength.HasValue)
            {
                arguments.Add("x-max-length", _maxQueueLength.Value);
                if (_hasDlq)
                {
                    arguments.Add("x-overflow", "reject-publish-dlx");
                }

                arguments.Add("x-overflow", "reject-publish");
            }

            return arguments;
        }

        private string GetDeadletterExchangeName()
        {
            return Connection.DeadLetterExchange == null
                ? Connection.Exchange.Name
                : Connection.DeadLetterExchange.Name;
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
    }
}
