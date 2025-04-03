using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Class MqttMessagePublisher .
    /// </summary>
    public class MqttMessagePublisher
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MQTTMessageProducer>();
        private readonly MqttMessagingGatewayConfiguration _config;
        private readonly IMqttClient _mqttClient;
        private readonly MqttClientOptions _mqttClientOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttMessagePublisher"/> class.
        /// Sync over async, but necessary as we are in the ctor
        /// </summary>
        /// <param name="config">The Publisher configuration.</param>
        public MqttMessagePublisher(MqttMessagingGatewayConfiguration config)
        {
            _config = config;

            _mqttClient = new MqttFactory().CreateMqttClient();

            MqttClientOptionsBuilder mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.Hostname, _config.Port)
                .WithCleanSession(_config.CleanSession);

            if (!string.IsNullOrEmpty(_config.ClientID))
            {
                mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithClientId($"{_config.ClientID}");
            }

            if (!string.IsNullOrEmpty(_config.Username))
            {
                mqttClientOptionsBuilder = mqttClientOptionsBuilder.WithCredentials(_config.Username, _config.Password);
            }

            _mqttClientOptions = mqttClientOptionsBuilder
                .WithTcpServer(config.Hostname, config.Port)
                .Build();

            ConnectAsync().GetAwaiter().GetResult();
        }

        private async Task ConnectAsync()
        {
            for (int i = 0; i < _config.ConnectionAttempts; i++)
            {
                try
                {
                    await _mqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
                    s_logger.LogInformation($"Connected to {_config.Hostname}:{_config.Port}");
                    return;
                }
                catch (Exception)
                {
                    s_logger.LogError($"Unable to connect to {_config.Hostname}:{_config.Port}");
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
            string payload = JsonSerializer.Serialize(message, JsonSerialisationOptions.Options);
            var builder = new MqttApplicationMessageBuilder()
                .WithTopic(_config.TopicPrefix != null ? $"{_config.TopicPrefix}/{message.Header.Topic}" : message.Header.Topic)
                .WithPayload(payload)
                //.WithContentType(message.Header.ContentType)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);

            //builder
            //    .WithUserProperty(HeaderNames.Id, message.Header.MessageId)
            //    .WithUserProperty(HeaderNames.Type, message.Header.Type)
            //    .WithUserProperty(HeaderNames.Time, message.Header.TimeStamp.ToRcf3339())
            //    .WithUserProperty(HeaderNames.Source, message.Header.Source.ToString())
            //    .WithUserProperty(HeaderNames.DataContentType, message.Header.ContentType)
            //    .WithUserProperty(HeaderNames.SpecVersion, message.Header.SpecVersion);

            if (message.Header.DataSchema != null)
            {
                builder.WithUserProperty(HeaderNames.DataSchema, message.Header.DataSchema.ToString());
            }

            if (!string.IsNullOrEmpty(message.Header.Subject))
            {
                builder.WithUserProperty(HeaderNames.Subject, message.Header.Subject);
            }

            return builder.Build();
        }
    }
}
