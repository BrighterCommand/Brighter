using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Class MqttMessageConsumer.
    /// The <see cref="MqttMessageConsumer"/> is used on the server to receive messages from the broker. It abstracts away the details of 
    /// inter-process communication tasks from the server. It handles subscription establishment, request reception and dispatching.
    /// </summary>
    public class MqttMessageConsumer : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
    {
        private readonly string _topic;
        private readonly Queue<Message> _messageQueue = new();
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MqttMessageConsumer>();
        private readonly Message _noopMessage = new();
        private readonly IMqttClient _mqttClient;
        private readonly MqttClientOptions _mqttClientOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttMessageConsumer"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The configuration settings for the MQTT message consumer, including connection details, 
        /// topic prefix, client credentials, and other options.
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
        public MqttMessageConsumer(MqttMessagingGatewayConsumerConfiguration configuration)
        {
            _topic = $"{configuration.TopicPrefix}/#" ?? throw new ArgumentNullException(nameof(configuration.TopicPrefix));

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
                s_logger.LogTrace("MqttMessageConsumer: Received message from queue {TopicPrefix}", configuration.TopicPrefix);
                var message = JsonSerializer.Deserialize<Message>(e.ApplicationMessage.PayloadSegment.ToArray(), JsonSerialisationOptions.Options);
             
                _messageQueue.Enqueue(message);
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
        public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }


        public void Dispose()
        {
            _mqttClient.Dispose();
        }


        public ValueTask DisposeAsync()
        {
            _mqttClient.Dispose();
            return new ValueTask(Task.CompletedTask);
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
            if (_messageQueue.Count == 0)
            {
                return new[] { _noopMessage };
            }

            var messages = new List<Message>();
            timeOut ??= TimeSpan.FromMilliseconds(300);

            using (var cts = new CancellationTokenSource(timeOut.Value))
            {
                cts.Token.Register(() => { throw new TimeoutException(); });
                while (!cts.IsCancellationRequested && _messageQueue.Count > 0)
                {
                    try
                    {
                        Message message = _messageQueue.Dequeue();
                        messages.Add(message);
                    }
                    catch (TimeoutException te)
                    {
                        s_logger.LogWarning(te, "MqttMessageConsumer: Timed out retrieving messages.  Queue length: {QueueLength}", _messageQueue.Count);
                    }
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
        public void Reject(Message message)
        {
        }

        /// <summary>
        /// Not implemented Reject Method.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="cancellationToken"></param>
        public Task RejectAsync(Message message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// Not implemented Requeue Method.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delay"></param>
        public bool Requeue(Message message, TimeSpan? delay = null)
        {
            return false;
        }

        public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        private async Task Connect(int connectionAttempts)
        {
            for (int i = 0; i < connectionAttempts; i++)
            {
                try
                {
                    await _mqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
                    s_logger.LogInformation("MQTT Consumer Client Connected");

                    await _mqttClient.SubscribeAsync(new MqttTopicFilter { Topic = _topic, QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce });
                    s_logger.LogInformation("Subscribed to {Topic}", _topic);

                    return;
                }
                catch (Exception ex)
                {
                    s_logger.LogError(ex, "Unable to connect MQTT Consumer Client");
                }
            }
        }
    }
}
