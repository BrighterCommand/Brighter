using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Class MqttMessageConsumer.
    /// The <see cref="MqttMessageConsumer"/> is used on the server to receive messages from the broker. It abstracts away the details of 
    /// inter-process communication tasks from the server. It handles subscription establishment, request reception and dispatching.
    /// </summary>
    public partial class MqttMessageConsumer : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
    {
        private readonly string _topic;
        private readonly MqttMessagingGatewayConsumerConfiguration _configuration;
        private readonly ConcurrentQueue<Message> _messageQueue = new();
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MqttMessageConsumer>();
        private readonly Message _noopMessage = new();
        private readonly IMqttClient _mqttClient;
        private readonly MqttClientOptions _mqttClientOptions;
        private readonly IAmAMessageScheduler? _scheduler;
        private MqttMessageProducer? _requeueProducer;
        private bool _requeueProducerInitialized;
        private object? _requeueProducerLock;

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttMessageConsumer"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The configuration settings for the MQTT message consumer, including connection details,
        /// topic prefix, client credentials, and other options.
        /// </param>
        /// <param name="scheduler">
        /// Optional scheduler for delayed message redelivery. When provided, the lazily-created
        /// requeue producer will use this scheduler for delayed sends.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the <paramref name="configuration.TopicPrefix"/> is null.
        /// </exception>
        /// <remarks>
        /// This constructor sets up the MQTT client with the provided configuration, establishes
        /// the connection to the broker, and subscribes to the specified topic.
        ///
        /// 04/03/2025:
        ///     - Removed support for user properties as they are not supported in v3.1.1 of the MQTT protocol.
        /// </remarks>
        public MqttMessageConsumer(MqttMessagingGatewayConsumerConfiguration configuration, IAmAMessageScheduler? scheduler = null)
        {
            _configuration = configuration;
            _scheduler = scheduler;
            ArgumentNullException.ThrowIfNull(configuration.TopicPrefix, nameof(configuration.TopicPrefix));
            _topic = $"{configuration.TopicPrefix}/#";

            MqttClientOptionsBuilder mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
               .WithTcpServer(configuration.Hostname)
               .WithCleanSession(configuration.CleanSession);

            if (!string.IsNullOrEmpty(configuration.ClientID))
            {
                mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithClientId($"{configuration.ClientID}");
            }

            if (!string.IsNullOrEmpty(configuration.Username))
            {
                mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithCredentials(configuration.Username, configuration.Password);
            }

            _mqttClientOptions = mqttClientOptionsBuilder
                .WithTcpServer(configuration.Hostname, configuration.Port)
                .Build();

            //TODO: Switch to using the low level client here, as it allows us explicit control over ack, receive etc.
            //This is slated for post V10, for now, we just want to upgrade this support the V10 release
            _mqttClient = new MqttFactory().CreateMqttClient();

            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                Log.MqttMessageConsumerReceivedMessage(s_logger, configuration.TopicPrefix);
                var message = JsonSerializer.Deserialize<Message>(e.ApplicationMessage.PayloadSegment.ToArray(), JsonSerialisationOptions.Options);

                _messageQueue.Enqueue(message!);
                return Task.CompletedTask;
            };

            Task connectTask = Connect(configuration.ConnectionAttempts);
            connectTask
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Not implemented Acknowledge Method.
        /// </summary>
        /// <param name="message"></param>
        public void Acknowledge(Message message)
        {
        }

        /// <summary>
        /// Not implemented Acknowledge Method.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="cancellationToken">Allows cancellation of the acknowledge operation</param>
        public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }


        public void Dispose()
        {
            _requeueProducer?.Dispose();
            _mqttClient.Dispose();
        }


        public async ValueTask DisposeAsync()
        {
            if (_requeueProducer != null) await _requeueProducer.DisposeAsync();
            // IMqttClient only implements IDisposable, not IAsyncDisposable (MQTTnet 4.3)
            _mqttClient.Dispose();
        }

        /// <summary>
        /// Clears the internal Queue buffer.
        /// </summary>
        public void Purge()
        {
            _messageQueue.Clear();
        }

        /// <summary>
        /// Clears the internal Queue buffer.
        /// </summary>
        /// <param name="cancellationToken">Allows cancellation of the purge task</param>
        public Task PurgeAsync(CancellationToken cancellationToken = default)
        {
            Purge();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves the current received messages from the internal buffer.
        /// </summary>
        /// <param name="timeOut">The time to delay retrieval. Defaults to 300ms</param>
        public Message[] Receive(TimeSpan? timeOut = null)
        {
            if (_messageQueue.IsEmpty)
            {
                return new[] { _noopMessage };
            }

            var messages = new List<Message>();
            timeOut ??= TimeSpan.FromMilliseconds(300);

            using (var cts = new CancellationTokenSource(timeOut.Value))
            {
                while (!cts.IsCancellationRequested && _messageQueue.TryDequeue(out var message))
                {
                    messages.Add(message);
                }
            }

            return messages.ToArray();
        }

        public Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Receive(timeOut));
        }

        /// <summary>
        /// Not implemented Reject Method.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
        public bool Reject(Message message, MessageRejectionReason? reason = null)
          => false;

        /// <summary>
        /// Not implemented Reject Method.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
        /// <param name="cancellationToken"></param>
        public Task<bool> RejectAsync(Message message,  MessageRejectionReason? reason = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);


        /// <summary>
        /// Requeues a message by publishing it back to the same topic via a lazily-created producer.
        /// When a delay is specified and a scheduler is configured, the producer delegates to the
        /// scheduler for delayed redelivery. For immediate requeue, the message is sent directly.
        /// </summary>
        /// <param name="message">The message to requeue.</param>
        /// <param name="delay">Optional delay before the message becomes available. Requires a scheduler when non-zero.</param>
        /// <returns><c>true</c> if the message was successfully requeued.</returns>
        public bool Requeue(Message message, TimeSpan? delay = null)
        {
            delay ??= TimeSpan.Zero;

            if (delay > TimeSpan.Zero)
            {
                EnsureRequeueProducer();
                _requeueProducer!.SendWithDelay(message, delay);
            }
            else
            {
                // MQTT is pub/sub — immediate requeue must publish back to the topic
                EnsureRequeueProducer();
                _requeueProducer!.Send(message);
            }

            return true;
        }

        /// <summary>
        /// Requeues a message asynchronously by publishing it back to the same topic via a lazily-created producer.
        /// When a delay is specified and a scheduler is configured, the producer delegates to the
        /// scheduler for delayed redelivery. For immediate requeue, the message is sent directly.
        /// </summary>
        /// <param name="message">The message to requeue.</param>
        /// <param name="delay">Optional delay before the message becomes available. Requires a scheduler when non-zero.</param>
        /// <param name="cancellationToken">Allows cancellation of the requeue operation.</param>
        /// <returns><c>true</c> if the message was successfully requeued.</returns>
        public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
            CancellationToken cancellationToken = default)
        {
            delay ??= TimeSpan.Zero;

            if (delay > TimeSpan.Zero)
            {
                EnsureRequeueProducer();
                await _requeueProducer!.SendWithDelayAsync(message, delay, cancellationToken);
            }
            else
            {
                // MQTT is pub/sub — immediate requeue must publish back to the topic
                EnsureRequeueProducer();
                await _requeueProducer!.SendAsync(message, cancellationToken);
            }

            return true;
        }

        /// <summary>
        /// Ensures a requeue producer exists, creating one lazily on first use.
        /// Uses <see cref="LazyInitializer.EnsureInitialized{T}(ref T, ref bool, ref object, Func{T})"/> for thread-safe initialization.
        /// </summary>
        private void EnsureRequeueProducer()
        {
            LazyInitializer.EnsureInitialized(ref _requeueProducer, ref _requeueProducerInitialized,
                ref _requeueProducerLock, () =>
                {
                    var publisher = new MqttMessagePublisher(new MqttMessagingGatewayProducerConfiguration
                    {
                        Hostname = _configuration.Hostname,
                        Port = _configuration.Port,
                        TopicPrefix = _configuration.TopicPrefix,
                        CleanSession = _configuration.CleanSession,
                        Username = _configuration.Username,
                        Password = _configuration.Password
                    });
                    return new MqttMessageProducer(publisher, new Publication())
                    {
                        Scheduler = _scheduler
                    };
                });
        }

        private async Task Connect(int connectionAttempts)
        {
            for (int i = 0; i < connectionAttempts; i++)
            {
                try
                {
                    await _mqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
                    Log.MqttConsumerClientConnected(s_logger);

                    await _mqttClient.SubscribeAsync(new MqttTopicFilter { Topic = _topic, QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce });
                    Log.SubscribedToTopic(s_logger, _topic);

                    return;
                }
                catch (Exception ex)
                {
                    Log.UnableToConnectMqttConsumerClient(s_logger, ex);
                }
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Trace, "MQTTMessageConsumer: Received message from queue {TopicPrefix}")]
            public static partial void MqttMessageConsumerReceivedMessage(ILogger logger, object? topicPrefix);

            [LoggerMessage(Level = LogLevel.Warning, Message = "MQTTMessageConsumer: Timed out retrieving messages.  Queue length: {QueueLength}")]
            public static partial void MqttMessageConsumerTimedOutRetrievingMessages(ILogger logger, Exception ex, int queueLength);

            [LoggerMessage(LogLevel.Information, "MQTT Consumer Client Connected")]
            public static partial void MqttConsumerClientConnected(ILogger logger);

            [LoggerMessage(LogLevel.Information, "Subscribed to {Topic}")]
            public static partial void SubscribedToTopic(ILogger logger, string topic);

            [LoggerMessage(Level = LogLevel.Error, Message = "Unable to connect MQTT Consumer Client")]
            public static partial void UnableToConnectMqttConsumerClient(ILogger logger, Exception ex);
        }
    }
}

