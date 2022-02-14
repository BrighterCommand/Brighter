using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Serialization;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    public class MQTTMessagePublisher
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MQTTMessageProducer>();
        private MQTTMessagingGatewayConfiguration _config;
        private IMqttClient mqttClient;
        private IMqttClientOptions mqttClientOptions;
        public MQTTMessagePublisher(MQTTMessagingGatewayConfiguration config)
        {
            _config = config;

            mqttClient = new MqttFactory().CreateMqttClient();

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

            mqttClientOptions = mqttClientOptionsBuilder.Build();

            Connect();
        }

        private void Connect()
        {
            for (int i = 0; i < _config.ConnectionAttempts; i++)
            {
                try
                {
                    mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None).GetAwaiter().GetResult();
                    s_logger.LogInformation($"Connected to {_config.Hostname}");
                    return;
                }
                catch (Exception)
                {
                    s_logger.LogError($"Unable to connect to {_config.Hostname}");
                }
            }
        }

        public void PublishMessage(Message message)
        {
            PublishMessageAsync(message).GetAwaiter().GetResult();
        }

        public async Task PublishMessageAsync(Message message)
        {
            MqttApplicationMessage mqttMessage = createMQTTMessage(message);
            await mqttClient.PublishAsync(mqttMessage, CancellationToken.None);
        }

        private MqttApplicationMessage createMQTTMessage(Message message)
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
