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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Paramore.Brighter.MessagingGateway.RMQ.Logging;
using Polly.CircuitBreaker;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Class RmqMessageConsumer.
    /// The <see cref="RmqMessageConsumer"/> is used on the server to receive messages from the broker. It abstracts away the details of 
    /// inter-process communication tasks from the server. It handles connection establishment, request reception and dispatching, 
    /// result sending, and error handling.
    /// </summary>
    public class RmqMessageConsumer : RMQMessageGateway, IAmAMessageConsumer
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RmqMessageConsumer>);

        private PullConsumer _consumer;
        private readonly string _queueName;
        private readonly RoutingKeys _routingKeys;
        private readonly bool _isDurable;
        private readonly int _batchSize;
        private readonly RmqMessageCreator _messageCreator;
        private readonly Message _noopMessage = new Message();

      /// <summary>
      /// Initializes a new instance of the <see cref="RMQMessageGateway" /> class.
      /// </summary>
      /// <param name="connection"></param>
      /// <param name="queueName">The queue name.</param>
      /// <param name="routingKey">The routing key.</param>
      /// <param name="isDurable">Is the queue definition persisted</param>
      /// <param name="highAvailability">Is the queue available on all nodes in a cluster</param>
      /// <param name="batchSize">How many messages to retrieve at one time; ought to be size of channel buffer</param>
      public RmqMessageConsumer(
            RmqMessagingGatewayConnection connection, 
            string queueName, 
            string routingKey, 
            bool isDurable, 
            bool highAvailability = false,
            int batchSize = 1) 
            : this(connection, queueName, new string[] {routingKey}, isDurable, highAvailability, batchSize)
        {}

      /// <summary>
      /// Initializes a new instance of the <see cref="RMQMessageGateway" /> class.
      /// </summary>
      /// <param name="connection"></param>
      /// <param name="queueName">The queue name.</param>
      /// <param name="routingKeys">The routing keys.</param>
      /// <param name="isDurable">Is the queue persisted to disk</param>
      /// <param name="highAvailability"></param>
      public RmqMessageConsumer(
            RmqMessagingGatewayConnection connection, 
            string queueName, 
            string[] routingKeys, 
            bool isDurable, 
            bool highAvailability = false,
            int batchSize = 1) 
            : base(connection, batchSize)
        {
            _queueName = queueName;
            _routingKeys = new RoutingKeys(routingKeys);
            _isDurable = isDurable;
            IsQueueMirroredAcrossAllNodesInTheCluster = highAvailability;
            _messageCreator = new RmqMessageCreator();
            _batchSize = batchSize;
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
                EnsureChannel(_queueName);
                _logger.Value.InfoFormat("RmqMessageConsumer: Acknowledging message {0} as completed with delivery tag {1}", message.Id, deliveryTag);
                Channel.BasicAck(deliveryTag, false);
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException("RmqMessageConsumer: Error acknowledging message {0} as completed with delivery tag {1}", exception, message.Id, deliveryTag);
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
                EnsureChannelBind();
                _logger.Value.DebugFormat("RmqMessageConsumer: Purging channel {0}", _queueName);

                try { Channel.QueuePurge(_queueName); }
                catch (OperationInterruptedException operationInterruptedException)
                {
                    if (operationInterruptedException.ShutdownReason.ReplyCode == 404) { return; }
                    throw;
                }
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException("RmqMessageConsumer: Error purging channel {0}", exception, _queueName);
                throw;
            }
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public void Requeue(Message message, int delayMilliseconds)
        {
            try
            {
                _logger.Value.DebugFormat("RmqMessageConsumer: Re-queueing message {0} with a delay of {1} milliseconds", message.Id, delayMilliseconds);
                EnsureChannel(_queueName);
                var rmqMessagePublisher = new RmqMessagePublisher(Channel, Connection.Exchange.Name);
                if (DelaySupported)
                {
                    rmqMessagePublisher.RequeueMessage(message, _queueName, delayMilliseconds);
                }
                else
                {
                    Task.Delay(delayMilliseconds).Wait();
                    rmqMessagePublisher.RequeueMessage(message, _queueName, 0);
                }

                Reject(message, false);
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException("RmqMessageConsumer: Error re-queueing message {0}", exception, message.Id);
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
                _logger.Value.InfoFormat("RmqMessageConsumer: NoAck message {0} with delivery tag {1}", message.Id, message.DeliveryTag);
                Channel.BasicNack(message.DeliveryTag, false, requeue);
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException("RmqMessageConsumer: Error try to NoAck message {0}", exception, message.Id);
                throw;
            }
        }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds. We retry at timeout /5 ms intervals, with a min of 5ms
        /// until the timeout value is reached. </param>
        /// <returns>Message.</returns>
        public Message[] Receive(int timeoutInMilliseconds)
        {
            _logger.Value.DebugFormat("RmqMessageConsumer: Preparing to retrieve next message from queue {0} with routing key {1} via exchange {2} on connection {3}", _queueName, _routingKeys, Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri());

            try
            {
                EnsureChannelBind();

                var (resultCount, results) = _consumer.DeQueue(timeoutInMilliseconds, _batchSize);

                if (results != null && results.Length != 0)
                {
                    var messages = new Message[resultCount];
                    for (var i = 0; i < resultCount; i++)
                    {
                        var message = _messageCreator.CreateMessage(results[i]);
                        messages[i] = message;
                        
                        _logger.Value.InfoFormat(
                            "RmqMessageConsumer: Received message from queue {0} with routing key {1} via exchange {2} on connection {3}, message: {5}{4}",
                            _queueName, _routingKeys, Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(),
                            JsonConvert.SerializeObject(message),
                            JsonConvert.SerializeObject(message),
                            Environment.NewLine);
                    }

                    return messages;

                }
                else
                {
                    return new Message[] {_noopMessage};
                }

            }
            catch (EndOfStreamException endOfStreamException)
            {
                _logger.Value.DebugException(
                    "RmqMessageConsumer: The model closed, or the connection went away. Listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                    endOfStreamException,
                    _queueName,
                    _routingKeys,
                    Connection.Exchange.Name,
                    Connection.AmpqUri.GetSanitizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", endOfStreamException);
             }
            catch (BrokerUnreachableException bue)
            {
                _logger.Value.ErrorException(
                    "RmqMessageConsumer: There broker was unreachable listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                    bue,
                    _queueName,
                    _routingKeys,
                    Connection.Exchange.Name,
                    Connection.AmpqUri.GetSanitizedUri());
                ResetConnectionToBroker();
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", bue);
            }
            catch (AlreadyClosedException ace)
            {
                _logger.Value.ErrorException(
                    "RmqMessageConsumer: There connection was already closed when listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                    ace,
                    _queueName,
                    _routingKeys,
                    Connection.Exchange.Name,
                    Connection.AmpqUri.GetSanitizedUri());
                ResetConnectionToBroker();
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", ace);
            }
            catch (OperationInterruptedException oie)
            {
                _logger.Value.ErrorException(
                    "RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                    oie,
                    _queueName,
                    _routingKeys,
                    Connection.Exchange.Name,
                    Connection.AmpqUri.GetSanitizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", oie);
            }
            catch (TimeoutException te)
            {
                 _logger.Value.ErrorException("RmqMessageConsumer: The socket timed out whilst listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                    te,
                    _queueName,
                    _routingKeys,
                    Connection.Exchange.Name,
                    Connection.AmpqUri.GetSanitizedUri());
                ResetConnectionToBroker();
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", te);
                
            }
            catch (NotSupportedException nse)
            {
                _logger.Value.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                    nse,
                    _queueName,
                    _routingKeys,
                    Connection.Exchange.Name,
                    Connection.AmpqUri.GetSanitizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", nse);
            }
            catch (BrokenCircuitException bce)
            {
                _logger.Value.WarnFormat("CIRCUIT BROKEN: RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}",
                    _queueName,
                    _routingKeys,
                    Connection.Exchange.Name,
                    Connection.AmpqUri.GetSanitizedUri());
                throw new ChannelFailureException("Error connecting to RabbitMQ, see inner exception for details", bce);
            }
            catch (Exception exception)
            {
                _logger.Value.ErrorException("RmqMessageConsumer: There was an error listening to queue {0} via exchange {1} via exchange {2} on connection {3}", exception, _queueName, _routingKeys, Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri());
                throw;
            }
        }

        protected virtual void CancelConsumer()
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

        protected virtual void CreateConsumer()
        {
            _consumer = new PullConsumer(Channel);
            
            Channel.BasicQos(0, (ushort)_batchSize, false);

            Channel.BasicConsume(_queueName, false, string.Empty, SetQueueArguments(), _consumer);
            
            _consumer.HandleBasicConsumeOk(String.Empty);
            
            _logger.Value.InfoFormat("RmqMessageConsumer: Created consumer for queue {0} with routing key {1} via exchange {2} on connection {3}",
                _queueName,
                _routingKeys,
                Connection.Exchange.Name,
                Connection.AmpqUri.GetSanitizedUri()
                );
        }
        

        protected virtual void EnsureChannelBind()
        {
          if (Channel == null || Channel.IsClosed)
          {
            EnsureChannel(_queueName);

            _logger.Value.DebugFormat("RmqMessageConsumer: Creating queue {0} on connection {1}", _queueName, Connection.AmpqUri.GetSanitizedUri());

            Channel.QueueDeclare(_queueName, _isDurable, false, false, SetQueueArguments());

            foreach (var key in _routingKeys)
            {
              Channel.QueueBind(_queueName, Connection.Exchange.Name, key);
            }
            
            CreateConsumer();

            _logger.Value.InfoFormat(
              "RmqMessageConsumer: Created rabbitmq channel {4} for queue {0} with routing key/s {1} via exchange {2} on connection {3}",
              _queueName,
              _routingKeys,
              Connection.Exchange.Name,
              Connection.AmpqUri.GetSanitizedUri(),
              Channel.ChannelNumber
            );
          }
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

        private bool IsQueueMirroredAcrossAllNodesInTheCluster { get; }

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

    internal class RoutingKeys : IEnumerable<string>
    {        
        private readonly IEnumerable<string> _routingKeys;

        public RoutingKeys(params string[] routingKeys)
        {
            _routingKeys = routingKeys;
        }                      

        public IEnumerator<string> GetEnumerator()
        {
            return _routingKeys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return $"[{string.Join(", ", _routingKeys)}]";
        }       
    }
}
