using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Class MqttMessagePublisher .
    /// </summary>
    public partial class MqttMessagePublisher : IDisposable, IAsyncDisposable
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MqttMessageProducer>();
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

        /// <summary>
        /// Creates an MQTT application message from the provided <see cref="Message"/> and topic prefix.
        /// </summary>
        /// <param name="message">
        /// The <see cref="Message"/> instance containing the header and body data to be included in the MQTT message.
        /// </param>
        /// <param name="topicPrefix">
        /// An optional prefix to be prepended to the topic of the MQTT message. If <c>null</c>, the topic will be derived solely from the message header.
        /// </param>
        /// <returns>
        /// An instance of <see cref="MqttApplicationMessage"/> configured with the serialized message payload, topic, and quality of service level.
        /// </returns>
        /// <remarks>
        /// This method serializes the message body using the options defined in <see cref="JsonSerialisationOptions.Options"/>.
        /// It also sets user properties for the MQTT message based on the message header, such as <c>DataSchema</c> and <c>Subject</c>, if available.
        ///
        /// 04/03/2025:
        ///     - Removed ContentType as it's not supported in v3.1.1 of the MQTT protocol.  Version 5.0 supports it, but we are not using it.
        ///     - Removed the user properties for Id, Type, Time, Source, DataContentType, SpecVersion, DataSchema, and Subject as user properties
        ///       are not supported in v3.1.1 of the MQTT protocol. Version 5.0 supports it, but we are not using it.
        /// </remarks>
        public static MqttApplicationMessage CreateMqttMessage(Message message, object? topicPrefix)
        {
            string payload = JsonSerializer.Serialize(message, JsonSerialisationOptions.Options);
            var builder = new MqttApplicationMessageBuilder()
                .WithTopic(topicPrefix != null ? $"{topicPrefix}/{message.Header.Topic}" : message.Header.Topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);

            return builder.Build();
        }


        /// <summary>
        /// Sends the specified message.
        /// Sync over async
        /// </summary>
        /// <param name="message">The message.</param>
        public void PublishMessage(Message message) => BrighterAsyncContext.Run(() => PublishMessageAsync(message));

        /// <summary>
        /// Sends the specified message asynchronously.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">Allows cancellation of the operation</param>
        /// <returns>Task.</returns>
        public async Task PublishMessageAsync(Message message, CancellationToken cancellationToken = default)
        {
            MqttApplicationMessage mqttMessage = CreateMqttMessage(message, _config.TopicPrefix);
            await _mqttClient.PublishAsync(mqttMessage, cancellationToken);
        }

        private async Task ConnectAsync()
        {
            for (int i = 0; i < _config.ConnectionAttempts; i++)
            {
                try
                {
                    await _mqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
                    Log.ConnectedToHost(s_logger, _config.Hostname, _config.Port);
                    return;
                }
                catch (Exception)
                {
                    Log.UnableToConnectToHost(s_logger, _config.Hostname!, _config.Port);
                }
            }
        }

        /// <summary>
        /// Disposes the underlying MQTT client connection.
        /// </summary>
        public void Dispose()
        {
            _mqttClient.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously disposes the underlying MQTT client connection.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_mqttClient.IsConnected)
                await _mqttClient.DisconnectAsync();
            _mqttClient.Dispose();
            GC.SuppressFinalize(this);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "Connected to {Hostname}:{Port}")]
            public static partial void ConnectedToHost(ILogger logger, string? hostname, int port);

            [LoggerMessage(LogLevel.Error, "Unable to connect to {Hostname}:{Port}")]
            public static partial void UnableToConnectToHost(ILogger logger, string hostname, int port);
        }
    }
}
