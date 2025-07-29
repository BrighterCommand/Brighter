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
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Polly.CircuitBreaker;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
{
    /// <summary>
    /// Class RmqMessageConsumer.
    /// The <see cref="RmqMessageConsumer"/> is used on the server to receive messages from the broker. It abstracts away the details of 
    /// inter-process communication tasks from the server. It handles subscription establishment, request reception and dispatching, 
    /// result sending, and error handling.
    /// <remarks>This version of the consumer supports the RMQ V6 Client and its blocking API. For support of the V7 non-blocking API, please use
    /// the package Paramore.Brighter.MessagingGateway.RMQ.Async.
    /// </remarks>
    /// </summary>
    public partial class RmqMessageConsumer : RmqMessageGateway, IAmAMessageConsumerSync
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageConsumer>();

        private PullConsumer? _consumer;
        private readonly ChannelName _queueName;
        private readonly RoutingKeys _routingKeys;
        private readonly bool _isDurable;
        private readonly Message _noopMessage = new Message();
        private readonly string _consumerTag;
        private readonly OnMissingChannel _makeChannels;
        private readonly ushort _batchSize;
        private readonly bool _highAvailability;
        private readonly ChannelName? _deadLetterQueueName;
        private readonly RoutingKey? _deadLetterRoutingKey;
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
            ChannelName? deadLetterQueueName = null,
            RoutingKey? deadLetterRoutingKey = null,
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
            ChannelName? deadLetterQueueName = null,
            RoutingKey? deadLetterRoutingKey = null,
            TimeSpan? ttl = null,
            int? maxQueueLength = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create)
            : base(connection)
        {
            _queueName = queueName;
            _routingKeys = routingKeys;
            _isDurable = isDurable;
            _highAvailability = highAvailability;
            _batchSize = Convert.ToUInt16(batchSize);
            _makeChannels = makeChannels;
            _consumerTag = Connection.Name + Uuid.New(); 
            _deadLetterQueueName = deadLetterQueueName;
            _deadLetterRoutingKey = deadLetterRoutingKey;
            //NOTE: Weird because netstandard20 can't understand that isnullor empty checks for null
            _hasDlq = !string.IsNullOrEmpty(deadLetterQueueName ?? string.Empty) && !string.IsNullOrEmpty(_deadLetterRoutingKey ?? string.Empty);
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
                Log.AcknowledgingMessage(s_logger, message.Id, deliveryTag);
                //NOTE: Ensure Broker will create a channel if it is not already created
                Channel!.BasicAck(deliveryTag, false);
            }
            catch (Exception exception)
            {
                Log.ErrorAcknowledgingMessage(s_logger, exception, message.Id, deliveryTag);
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
                Log.PurgingChannel(s_logger, _queueName.Value);

                //NOTE: Ensure Broker will create a channel if it is not already created
                try { Channel!.QueuePurge(_queueName.Value); }
                catch (OperationInterruptedException operationInterruptedException)
                {
                    if (operationInterruptedException.ShutdownReason.ReplyCode == 404) { return; }

                    throw;
                }
            }
            catch (Exception exception)
            {
                Log.ErrorPurgingChannel(s_logger, exception, _queueName.Value);
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
                Log.RequeueingMessage(s_logger, message.Id, timeout.Value.TotalMilliseconds);
                EnsureBroker(_queueName);

                //Ensure Broker will create a channel if it is not already created
                var rmqMessagePublisher = new RmqMessagePublisher(Channel!, Connection);
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
                Log.DeletingMessage(s_logger, message.Id, deliveryTag);
                
                //NOTE: Ensure Broker will create a channel if it is not already created
                Channel!.BasicAck(deliveryTag, false);

                return true;
            }
            catch (Exception exception)
            {
                Log.ErrorRequeueingMessage(s_logger, exception, message.Id);
                return false;
            }
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public bool Reject(Message message)
        {
            try
            {
                EnsureBroker(_queueName);
                Log.NoAckMessage(s_logger, message.Id, message.DeliveryTag);
                //if we have a DLQ, this will force over to the DLQ
                Channel!.BasicReject(message.DeliveryTag, false);
                return true;
            }
            catch (Exception exception)
            {
                Log.ErrorNoAckMessage(s_logger, exception, message.Id);
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
           
            if (Connection.Exchange is null)
                throw new InvalidOperationException("RmqMessageConsumer.Receive - value of Connection.Exchange cannot be null");
            
            if (Connection.AmpqUri is null)
                throw new InvalidOperationException("RmqMessageConsumer.Receive - value of Connection.AmpqUri cannot be null");

            Log.PreparingToRetrieveMessage(s_logger, _queueName.Value, string.Join(";", _routingKeys.Select(rk => rk.Value)), Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri());
            
            timeOut ??= TimeSpan.FromMilliseconds(5);

            try
            {
                EnsureChannel();

                //NOTE: EnsureChannel means that _consumer cannot be null
                var (resultCount, results) = _consumer!.DeQueue(timeOut.Value, _batchSize);

                if (results != null && results.Length != 0)
                {
                    var messages = new Message[resultCount];
                    for (var i = 0; i < resultCount; i++)
                    {
                        var message = RmqMessageCreator.CreateMessage(results[i]);
                        messages[i] = message;

                        Log.ReceivedMessage(s_logger, _queueName.Value, string.Join(";", _routingKeys.Select(rk => rk.Value)), Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(), JsonSerializer.Serialize(message, JsonSerialisationOptions.Options));
                    }

                    return messages;
                }
                else
                {
                    
                    return [_noopMessage];
                }
            }
            catch (Exception exception) when (exception is BrokerUnreachableException ||
                                              exception is AlreadyClosedException ||
                                              exception is TimeoutException)
            {
                HandleException(exception, true);
            }
            catch (Exception exception) when (exception is EndOfStreamException ||
                                              exception is OperationInterruptedException ||
                                              exception is NotSupportedException ||
                                              exception is BrokenCircuitException)
            {
                HandleException(exception);
            }
            catch (Exception exception)
            {
                HandleException(exception);
            }

            return [_noopMessage]; // Default return in case of exception
        }

        protected virtual void EnsureChannel()
        {
            if (Connection.Exchange is null)
                throw new InvalidOperationException("RmqMessageConsumer.EnsureChannel - value of Connection.Exchange cannot be null");
            
            if (Connection.AmpqUri is null)
                throw new InvalidOperationException("RmqMessageConsumer.EnsureChannel - value of Connection.AmpqUri cannot be null");
            
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

                Log.CreatedChannel(s_logger, Channel!.ChannelNumber, _queueName.Value, string.Join(";", _routingKeys.Select(rk => rk.Value)), Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri());
            }
        }

        private void CancelConsumer()
        {
            if (_consumer != null)
            {
                if (_consumer.IsRunning && Channel != null)
                {
                    Channel.BasicCancel(_consumerTag);
                }

                _consumer = null;
            }
        }

        private void CreateConsumer()
        {
            if (Channel == null)
                throw new InvalidOperationException("RmqMessageConsumer.CreateConsumer - value of Channel cannot be null");
            
            if (Connection.Exchange is null)
                throw new InvalidOperationException("RmqMessageConsumer.CreateConsumer - value of Connection.Exchange cannot be null");
            
            if (Connection.AmpqUri is null)
                throw new InvalidOperationException("RmqMessageConsumer.CreateConsumer - value of Connection.AmpqUri cannot be null");
            
            _consumer = new PullConsumer(Channel, _batchSize);

            Channel.BasicConsume(_queueName.Value, false, _consumerTag, false, false, SetQueueArguments(), _consumer);

            _consumer.HandleBasicConsumeOk(_consumerTag);

            Log.CreatedConsumer(s_logger, _queueName.Value, string.Join(";", _routingKeys.Select(rk => rk.Value)), Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri());
        }

        private void CreateQueue()
        {
            if (Channel == null)
                throw new InvalidOperationException("RmqMessageConsumer.CreateQueue - value of Channel cannot be null");
            
            if (Connection.AmpqUri is null)
                throw new InvalidOperationException("RmqMessageConsumer.CreateQueue - value of Connection.AmpqUri cannot be null");
            
            Log.CreatingQueue(s_logger, _queueName.Value, Connection.AmpqUri.GetSanitizedUri());
            Channel.QueueDeclare(_queueName.Value, _isDurable, false, false, SetQueueArguments());
            //NOTE: hasDlq cannot be true if _deadLetterQueuename is null
            if (_hasDlq) Channel.QueueDeclare(_deadLetterQueueName!.Value, _isDurable, false, false, new Dictionary<string, object>());
        }

        private void BindQueue()
        {
            if (Channel == null)
                throw new InvalidOperationException("RmqMessageConsumer.BindQueue - value of Channel cannot be null");
            
            if (Connection.Exchange is null)
                throw new InvalidOperationException("RmqMessageConsumer.BindQueue - value of Connection.Exchange cannot be null");
            
            foreach (var key in _routingKeys)
            {
                Channel.QueueBind(_queueName.Value, Connection.Exchange.Name, key, new Dictionary<string, object>());
            }

            if (_hasDlq)
                //NOTE: hasDlq cannot be true if _deadLetterQueuename -r _deadLetterRoutingKey is null
                Channel.QueueBind(_deadLetterQueueName!.Value, GetDeadletterExchangeName(), _deadLetterRoutingKey!.Value, new Dictionary<string, object>());
        }

        private void HandleException(Exception exception, bool resetConnection = false)
        {
            Log.ErrorListeningToQueue(s_logger, exception, _queueName.Value, string.Join(";", _routingKeys.Select(rk => rk.Value)), Connection.Exchange?.Name ?? string.Empty, Connection.AmpqUri?.GetSanitizedUri() ?? string.Empty);
            if (resetConnection) ResetConnectionToBroker();
            throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", exception);
        }
        
        private void ValidateQueue()
        {
            if (Channel == null)
                throw new InvalidOperationException("RmqMessageConsumer.ValidateQueue - value of Channel cannot be null");
            
            Log.ValidatingQueue(s_logger, _queueName.Value, Connection.AmpqUri!.GetSanitizedUri());

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
                //NOTE: hasDlq cannot be true if _deadLetterQueuename -r _deadLetterRoutingKey is null
                arguments.Add("x-dead-letter-exchange", GetDeadletterExchangeName());
                arguments.Add("x-dead-letter-routing-key", _deadLetterRoutingKey!.Value);
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
                ? Connection.Exchange!.Name
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

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "RmqMessageConsumer: Acknowledging message {Id} as completed with delivery tag {DeliveryTag}")]
            public static partial void AcknowledgingMessage(ILogger logger, string id, ulong deliveryTag);

            [LoggerMessage(LogLevel.Error, "RmqMessageConsumer: Error acknowledging message {Id} as completed with delivery tag {DeliveryTag}")]
            public static partial void ErrorAcknowledgingMessage(ILogger logger, Exception exception, string id, ulong deliveryTag);

            [LoggerMessage(LogLevel.Debug, "RmqMessageConsumer: Purging channel {ChannelName}")]
            public static partial void PurgingChannel(ILogger logger, string channelName);

            [LoggerMessage(LogLevel.Error, "RmqMessageConsumer: Error purging channel {ChannelName}")]
            public static partial void ErrorPurgingChannel(ILogger logger, Exception exception, string channelName);

            [LoggerMessage(LogLevel.Debug, "RmqMessageConsumer: Re-queueing message {Id} with a delay of {Delay} milliseconds")]
            public static partial void RequeueingMessage(ILogger logger, string id, double delay);

            [LoggerMessage(LogLevel.Information, "RmqMessageConsumer: Deleting message {Id} with delivery tag {DeliveryTag} as re-queued")]
            public static partial void DeletingMessage(ILogger logger, string id, ulong deliveryTag);

            [LoggerMessage(LogLevel.Error, "RmqMessageConsumer: Error re-queueing message {Id}")]
            public static partial void ErrorRequeueingMessage(ILogger logger, Exception exception, string id);

            [LoggerMessage(LogLevel.Information, "RmqMessageConsumer: NoAck message {Id} with delivery tag {DeliveryTag}")]
            public static partial void NoAckMessage(ILogger logger, string id, ulong deliveryTag);

            [LoggerMessage(LogLevel.Error, "RmqMessageConsumer: Error try to NoAck message {Id}")]
            public static partial void ErrorNoAckMessage(ILogger logger, Exception exception, string id);

            [LoggerMessage(LogLevel.Debug, "RmqMessageConsumer: Preparing to retrieve next message from queue {ChannelName} with routing key {RoutingKeys} via exchange {ExchangeName} on subscription {URL}")]
            public static partial void PreparingToRetrieveMessage(ILogger logger, string channelName, string routingKeys, string exchangeName, string url);

            [LoggerMessage(LogLevel.Information, "RmqMessageConsumer: Received message from queue {ChannelName} with routing key {RoutingKeys} via exchange {ExchangeName} on subscription {URL}, message: {Request}")]
            public static partial void ReceivedMessage(ILogger logger, string channelName, string routingKeys, string exchangeName, string url, string request);


            [LoggerMessage(LogLevel.Information, "RmqMessageConsumer: Created rabbitmq channel {ConsumerNumber} for queue {ChannelName} with routing key/s {RoutingKeys} via exchange {ExchangeName} on subscription {URL}")]
            public static partial void CreatedChannel(ILogger logger, long consumerNumber, string channelName, string routingKeys, string exchangeName, string url);

            [LoggerMessage(LogLevel.Information, "RmqMessageConsumer: Created consumer for queue {ChannelName} with routing key {Topic} via exchange {ExchangeName} on subscription {URL}")]
            public static partial void CreatedConsumer(ILogger logger, string channelName, string topic, string exchangeName, string url);

            [LoggerMessage(LogLevel.Debug, "RmqMessageConsumer: Creating queue {ChannelName} on subscription {URL}")]
            public static partial void CreatingQueue(ILogger logger, string channelName, string url);

            [LoggerMessage(LogLevel.Error, "RmqMessageConsumer: There was an error listening to queue {ChannelName} via exchange {RoutingKeys} via exchange {ExchangeName} on subscription {URL}")]
            public static partial void ErrorListeningToQueue(ILogger logger, Exception exception, string channelName, string routingKeys, string exchangeName, string url);
            
            [LoggerMessage(LogLevel.Debug, "RmqMessageConsumer: Validating queue {ChannelName} on subscription {URL}")]
            public static partial void ValidatingQueue(ILogger logger, string channelName, string url);
        }
    }
}

