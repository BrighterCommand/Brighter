using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class MQTTMessageConsumer : IAmAMessageConsumer
    {
        private readonly string _topic;
        private readonly Queue<Message> _messageQueue;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MQTTMessageConsumer>();
        private readonly Message _noopMessage = new Message();
        private readonly IMqttClientOptions _mqttClientOptions;
        private readonly IMqttClient _mqttClient;

        public MQTTMessagingGatewayConsumerConfiguration _config { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="topic"></param>
        public MQTTMessageConsumer(MQTTMessagingGatewayConsumerConfiguration configuration)
        {
            _topic = $"{configuration.TopicPrefix}/#" ?? throw new ArgumentNullException(nameof(configuration.TopicPrefix));
            _messageQueue = configuration.queue ?? new Queue<Message>();
            _config = configuration;

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

            _mqttClient = new MqttFactory().CreateMqttClient();

            _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(e =>
            {
                Console.WriteLine("Recieved");
                string message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                _messageQueue.Enqueue(JsonSerializer.Deserialize<Message>(message));
            });

            Task connectTask = Connect();
            connectTask.Wait();
        }

        public void Acknowledge(Message message)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _mqttClient.Dispose();
        }

        public void Purge()
        {
            _messageQueue.Clear();
        }

        public Message[] Receive(int timeoutInMilliseconds)
        {
            if (_messageQueue.Count==0)
                return new Message[] { _noopMessage };

            //s_logger.LogInformation(
            //    "RmqMessageConsumer: Received message from queue {ChannelName} with routing key {RoutingKeys} via exchange {ExchangeName} on subscription {URL}, message: {Request}",
            //    _queueName, _routingKeys, Connection.Exchange.Name, Connection.AmpqUri.GetSanitizedUri(),
            //    JsonSerializer.Serialize(message, JsonSerialisationOptions.Options),
            //    Environment.NewLine);

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

        public void Reject(Message message)
        {
            throw new NotImplementedException();
        }

        public bool Requeue(Message message, int delayMilliseconds)
        {
            throw new NotImplementedException();
        }

        private async Task Connect()
        {
            for (int i = 0; i < _config.ConnectionAttempts; i++)
            {
                try
                {
                   
                    await _mqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
                    s_logger.LogInformation($"Connected to {_config.Hostname}");

                    await _mqttClient.SubscribeAsync(new MqttTopicFilter { Topic = _topic, QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce });
                    s_logger.LogInformation($"Subscribed to #");

                    return;
                }
                catch (Exception)
                {
                    s_logger.LogError($"Unable to connect to {_config.Hostname}");
                }
            }
        }
    }
}
