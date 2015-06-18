// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 07-29-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright ? 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ?Software?), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ?AS IS?, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Polly;
using RabbitMQ.Client;

using paramore.brighter.commandprocessor.extensions;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class RMQMessageGateway.
    /// Base class for messaging gateway used by a <see cref="InputChannel"/> to communicate with a RabbitMQ server, to consume messages from the server or
    /// <see cref="CommandProcessor.Post{T}"/> to send a message to the RabbitMQ server. 
    /// A channel is associated with a queue name, which binds to a <see cref="MessageHeader.Topic"/> when <see cref="CommandProcessor.Post{T}"/> sends over a task queue. 
    /// So to listen for messages on that Topic you need to bind to the matching queue name. 
    /// The configuration holds a &lt;serviceActivatorConnections&gt; section which in turn contains a &lt;connections&gt; collection that contains a set of connections. 
    /// Each connection identifies a mapping between a queue name and a <see cref="IRequest"/> derived type. At runtime we read this list and listen on the associated channels.
    /// The <see cref="MessagePump"/> then uses the <see cref="IAmAMessageMapper"/> associated with the configured request type in <see cref="IAmAMessageMapperRegistry"/> to translate between the 
    /// on-the-wire message and the <see cref="Command"/> or <see cref="Event"/>
    /// </summary>
    public class MessageGateway : IDisposable
    {
        private static string[] HeadersToReset = { HeaderNames.DELAY_MILLISECONDS, HeaderNames.MESSAGE_TYPE, HeaderNames.TOPIC, HeaderNames.HANDLED_COUNT };

        private static uint LockTimeoutMilliseconds = 1000;
        private static readonly object s_lock = new object();

        private readonly ConnectionFactory _connectionFactory;
        private readonly ContextualPolicy _retryPolicy;
        private readonly Policy _circuitBreakerPolicy;

        private IModel _channel;

        protected readonly string _queueName;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public MessageGateway(ILog logger, string queueName)
        {
            Logger = logger;
            Configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();

            var connectionPolicyFactory = new ConnectionPolicyFactory(logger);

            _retryPolicy = connectionPolicyFactory.RetryPolicy;
            _circuitBreakerPolicy = connectionPolicyFactory.CircuitBreakerPolicy;

            _connectionFactory = new ConnectionFactory { Uri = Configuration.AMPQUri.Uri.ToString(), RequestedHeartbeat = 30 };

            DelaySupported = (this is IAmAMessageGatewaySupportingDelay) && Configuration.Exchange.SupportDelay;

            _queueName = queueName;
        }

        /// <summary>
        /// The logger
        /// </summary>
        protected readonly ILog Logger;

        /// <summary>
        /// The configuration
        /// </summary>
        protected readonly RMQMessagingGatewayConfigurationSection Configuration;

        /// <summary>
        /// Gets if the current provider configuration is able to support delayed delivery of messages.
        /// </summary>
        public bool DelaySupported { get; private set; }

        /// <summary>
        /// Gets if the channel has been initialised.
        /// </summary>
        public bool ChannelIsInitialized { get { return _channel != null; } }

        /// <summary>
        /// Connects the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        protected void EnsureChannel()
        {
            ConnectWithCircuitBreaker();
        }

        private void ConnectWithCircuitBreaker()
        {
            _circuitBreakerPolicy.Execute(() => ConnectWithRetry());
        }

        private void ConnectWithRetry()
        {
            _retryPolicy.Execute(() => ConnectToBroker(), new Dictionary<string, object> { { "queueName", _queueName } });
        }

        protected void Channel(Action<IModel> modelFunc)
        {
            EnsureChannel();

            _retryPolicy.Execute(() =>
                    ExecuteOnChannelAsThreadUnsafe(c =>
                    {
                        modelFunc.Invoke(_channel);
                    }));
        }

        protected void PublishMessage(Message message, int delayMilliseconds, bool regenerate = false)
        {
            EnsureChannel();

            _retryPolicy.Execute(() =>
                    ExecuteOnChannelAsThreadUnsafe(c =>
                    {
                        var messageId = regenerate ? Guid.NewGuid() : message.Id;
                        var deliveryTag = regenerate ? "1" : message.Header.Bag.ContainsKey(HeaderNames.DELIVERY_TAG) ? message.GetDeliveryTag().ToString() : null;

                        if (regenerate)
                            Logger.InfoFormat("RmqMessagePublisher: Regenerating message {0} with DeliveryTag of {1} to {2} with DeliveryTag of {3}", message.Id, deliveryTag ?? "(none)", messageId, 1);

                        var headers = new Dictionary<string, object>
                                                  {
                                                      {HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString()},
                                                      {HeaderNames.TOPIC, message.Header.Topic},
                                                      {HeaderNames.HANDLED_COUNT, message.Header.HandledCount.ToString(CultureInfo.InvariantCulture)},
                                                  };

                        message.Header.Bag.Each((header) =>
                        {
                            if (!HeadersToReset.Any(htr => htr.Equals(header.Key))) headers.Add(header.Key, header.Value);
                        });

                        if (!String.IsNullOrEmpty(deliveryTag))
                            headers.Add(HeaderNames.DELIVERY_TAG, deliveryTag);

                        if (delayMilliseconds > 0)
                            headers.Add(HeaderNames.DELAY_MILLISECONDS, delayMilliseconds);

                        if (regenerate && !message.Header.Bag.Any(h => h.Key.Equals(HeaderNames.ORIGINAL_MESSAGE_ID, StringComparison.CurrentCultureIgnoreCase)))
                            headers.Add(HeaderNames.ORIGINAL_MESSAGE_ID, message.Id.ToString());

                        _channel.BasicPublish(
                            Configuration.Exchange.Name,
                            message.Header.Topic,
                            false,
                            false,
                            CreateBasicProperties(messageId, message.Header.TimeStamp, headers),
                            Encoding.UTF8.GetBytes(message.Body.Value));
            }));
        }

        protected QueueingBasicConsumer CreateQueueingConsumer()
        {
            return new QueueingBasicConsumer(_channel);
        }

        protected virtual void ConnectToBroker()
        {
            if (_channel == null || _channel.IsClosed)
            {
                var connection = new MessageGatewayConnectionPool().GetConnection(_connectionFactory);

                Logger.DebugFormat("RMQMessagingGateway: Opening channel to Rabbit MQ on connection {0}", Configuration.AMPQUri.GetSanitizedUri());

                _channel = connection.CreateModel();

                //When AutoClose is true, the last channel to close will also cause the connection to close1. If it is set to
                //true before any channel is created, the connection will close then and there.
                if (connection.AutoClose == false)
                    connection.AutoClose = true;

                _retryPolicy.Execute(() =>
                    ExecuteOnChannelAsThreadUnsafe(c =>
                    {
                        Logger.DebugFormat("RMQMessagingGateway: Configuring QosPrefetchSize of {0} on connection {1}", Configuration.Queues.QosPrefetchSize, Configuration.AMPQUri.GetSanitizedUri());

                        // Configure the Quality of service for the model.
                        // BasicQos(0="Don't send me a new message until I?ve finished",  1= "Send me one message at a time", false ="Applied separately to each new consumer on the channel")
                        c.BasicQos(0, Configuration.Queues.QosPrefetchSize, false);

                        Logger.DebugFormat("RMQMessagingGateway: Declaring exchange {0} on connection {1}", Configuration.Exchange.Name, Configuration.AMPQUri.GetSanitizedUri());

                        //desired state configuration of the exchange
                        c.DeclareExchangeForConfiguration(Configuration);
                    })
                , new Dictionary<string, object> { { "queueName", _queueName } });
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MessageGateway()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_channel != null)
                {
                    _channel.Abort();
                    _channel = null;
                }
            }
        }

        private IBasicProperties CreateBasicProperties(Guid id, DateTime timeStamp, IDictionary<string, object> headers = null)
        {
            var basicProperties = _channel.CreateBasicProperties();

            basicProperties.DeliveryMode = 1;
            basicProperties.ContentType = "text/plain";
            basicProperties.MessageId = id.ToString();
            basicProperties.Timestamp = new AmqpTimestamp(UnixTimestamp.GetUnixTimestampSeconds(timeStamp));

            if (headers != null && headers.Any())
                basicProperties.Headers = headers;

            return basicProperties;
        }

        private void ExecuteOnChannelAsThreadUnsafe(Action<IModel> channelAction)
        {
            try
            {
                TimedLock timeLock = TimedLock.Lock(s_lock, TimeSpan.FromMilliseconds(LockTimeoutMilliseconds));
                channelAction.Invoke(_channel);
                timeLock.Dispose();
            }
            catch (LockTimeoutException e)
            {
/*#if DEBUG
                StackTrace otherStack = e.GetBlockingStackTrace(5000);
#endif*/

                Logger.InfoException(
                    "RMQMessagingGateway: Couldn't acquire lock on thread-unsafe Channel operation on queue {0} exchange {1} on connection {2}.",
                    e,
                    _queueName,
                    Configuration.Exchange.Name,
                    Configuration.AMPQUri.GetSanitizedUri());
            }
        }
    }
}