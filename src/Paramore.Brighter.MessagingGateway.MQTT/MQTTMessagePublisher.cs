using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Class MQTTMessagePublisher .
    /// </summary>
    public class MQTTMessagePublisher
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MQTTMessageProducer>();
        private readonly MQTTMessagingGatewayConfiguration _config;
        private readonly IMqttClient _mqttClient;
        private readonly IMqttClientOptions _mqttClientOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="MQTTMessagePublisher"/> class.
        /// Sync over async, but necessary as we are in the ctor
        /// </summary>
        /// <param name="config">The Publisher configuration.</param>
        public MQTTMessagePublisher(MQTTMessagingGatewayConfiguration config)
        {
            _config = config;

            _mqttClient = new MqttFactory().CreateMqttClient();

            MqttClientOptionsBuilder mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.Hostname)
                .WithCleanSession(_config.CleanSession);

            if (!string.IsNullOrEmpty(_config.ClientID))
            {
                mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithClientId($"{_config.ClientID}");
            }

            if (!string.IsNullOrEmpty(_config.Username))
            {
                mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithCredentials(_config.Username, _config.Password);
            }

            _mqttClientOptions = mqttClientOptionsBuilder.Build();

            ConnectAsync().GetAwaiter().GetResult();
        }

        private async Task ConnectAsync()
        {
            for (int i = 0; i < _config.ConnectionAttempts; i++)
            {
                try
                {
                    await _mqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
                    s_logger.LogInformation($"Connected to {_config.Hostname}");
                    return;
                }
                catch (Exception)
                {
                    s_logger.LogError($"Unable to connect to {_config.Hostname}");
                }
            }
        }

        /// <summary>
        /// Sends the specified message.
        /// Sync over async
        /// </summary>
        /// <param name="message">The message.</param>
        public void PublishMessage(Message message) => BrighterAsyncContext.Run(async () => await PublishMessageAsync(message));

        /// <summary>
        /// Sends the specified message asynchronously.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">Allows cancellation of the operation</param>
        /// <returns>Task.</returns>
        public async Task PublishMessageAsync(Message message, CancellationToken cancellationToken = default)
        {
            MqttApplicationMessage mqttMessage = CreateMqttMessage(message);
            await _mqttClient.PublishAsync(mqttMessage, cancellationToken);
        }

        private MqttApplicationMessage CreateMqttMessage(Message message)
        {
            string payload = JsonSerializer.Serialize(message);
            MqttApplicationMessageBuilder outMessage = new MqttApplicationMessageBuilder()
                 .WithTopic(_config.TopicPrefix!=null?
            $"{_config.TopicPrefix}/{message.Header.Topic}": message.Header.Topic)
                 .WithPayload(payload)
                 .WithAtLeastOnceQoS();
            return outMessage.Build();
        }
    }
}
