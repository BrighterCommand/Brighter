using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Protocol;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Class MQTTMessageConsumer.
    /// The <see cref="MQTTMessageConsumer"/> is used on the server to receive messages from the broker. It abstracts away the details of 
    /// inter-process communication tasks from the server. It handles subscription establishment, request reception and dispatching.
    /// </summary>
    public class MQTTMessageConsumer : IAmAMessageConsumer
    {
        private readonly string _topic;
        private readonly Queue<Message> _messageQueue = new Queue<Message>();
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MQTTMessageConsumer>();
        private readonly Message _noopMessage = new Message();
        private readonly IMqttClientOptions _mqttClientOptions;
        private readonly IMqttClient _mqttClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="MQTTMessageConsumer" /> class.
        /// </summary>
        /// <param name="configuration"></param>
        public MQTTMessageConsumer(MQTTMessagingGatewayConsumerConfiguration configuration)
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

            _mqttClientOptions = mqttClientOptionsBuilder.Build();

            _mqttClient = new MqttFactory().CreateMqttClient();

            _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(e =>
            {
                s_logger.LogInformation("MQTTMessageConsumer: Received message from queue {TopicPrefix}", configuration.TopicPrefix);
                string message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                _messageQueue.Enqueue(JsonSerializer.Deserialize<Message>(message));
            });

            Task connectTask = Connect(configuration.ConnectionAttempts);
            connectTask.Wait();
        }

        /// <summary>
        /// Not implemented Acknowledge Method.
        /// </summary>
        /// <param name="message"></param>
        public void Acknowledge(Message message)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
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
        /// Retrieves the current recieved messages from the internal buffer.
        /// </summary>
        /// <param name="timeoutInMilliseconds"></param>
        public Message[] Receive(int timeoutInMilliseconds)
        {
            if (_messageQueue.Count==0)
                return new Message[] { _noopMessage };

            List<Message> messages = new List<Message>();

            using (CancellationTokenSource cts = new CancellationTokenSource(timeoutInMilliseconds))
            {
                cts.Token.Register(() => { throw new TimeoutException(); });
                while (!cts.IsCancellationRequested && _messageQueue.Count > 0)
                {
                    try
                    {
                        Message message = _messageQueue.Dequeue();
                        messages.Add(message);
                    }
                    catch (TimeoutException)
                    {
                        s_logger.LogWarning("MQTTMessageConsumer: Timed out retrieving messages.  Queue length: {QueueLength}", _messageQueue.Count);
                    }
                }
            }

            return messages.ToArray();
        }

        /// <summary>
        /// Not implemented Reject Method.
        /// </summary>
        /// <param name="message"></param>
        public void Reject(Message message)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Not implemented Requeue Method.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds"></param>
        public bool Requeue(Message message, int delayMilliseconds)
        {
            throw new NotImplementedException();
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
                catch (Exception)
                {
                    s_logger.LogError("Unable to connect MQTT Consumer Client");
                }
            }
        }
    }
}
