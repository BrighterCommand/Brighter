using System;
using System.Net;
using MQTTnet;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MQTT;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Server;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Base
{
    /// <summary>
    /// Serves as a base class for MQTT-related test classes, providing common functionality for setting up
    /// and managing MQTT messaging infrastructure during tests.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the derived test class. This is used for logging and other type-specific configurations.
    /// </typeparam>
    /// <remarks>
    /// This class initializes an MQTT test server, configures message producers and consumers, and provides
    /// utility methods for managing the lifecycle of these components during tests.
    /// </remarks>
    public abstract class MqttTestClassBase<T> : IDisposable
        where T : class
    {
        private static readonly MqttFactory s_mqttFactory = new();

        protected readonly Message _noopMessage = new();
        private readonly MqttTestServer? _mqttTestServer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttTestClassBase{T}"/> class with the specified client ID, topic prefix, and test output helper.
        /// </summary>
        /// <param name="clientId">The unique identifier for the MQTT client.</param>
        /// <param name="topicPrefix">The prefix for MQTT topics used in the test.</param>
        /// <remarks>
        /// This constructor sets up the necessary MQTT test server and configurations for messaging gateway tests.
        /// It also configures logging to integrate with the test output helper.
        /// </remarks>
        protected MqttTestClassBase(string clientId, string topicPrefix)
        {
            IPAddress serverIpAddress = IPAddress.Any;
            int serverPort = MqttTestServer.GetRandomServerPort();

            _mqttTestServer = MqttTestServer.CreateTestMqttServer(s_mqttFactory, true, ApplicationLogging.CreateLogger<T>(), serverIpAddress, serverPort);

            var mqttProducerConfig = new MqttMessagingGatewayProducerConfiguration
            {
                Hostname = IPAddress.Loopback.ToString(),
                Port = serverPort,
                TopicPrefix = topicPrefix
            };

            MqttMessagePublisher mqttMessagePublisher = new(mqttProducerConfig);
            MessageProducerAsync = new MqttMessageProducer(mqttMessagePublisher, new Publication());

            MqttMessagingGatewayConsumerConfiguration mqttConsumerConfig = new()
            {
                Hostname = IPAddress.Loopback.ToString(),
                Port = serverPort,
                TopicPrefix = topicPrefix,
                ClientID = clientId
            };

            MessageConsumerAsync = new MqttMessageConsumer(mqttConsumerConfig);
        }

        /// <summary>
        /// Gets the asynchronous message producer used for sending messages to the MQTT messaging gateway during tests.
        /// </summary>
        /// <remarks>
        /// This property provides an instance of <see cref="IAmAMessageProducerAsync"/>, which is used to send messages
        /// asynchronously to the configured MQTT messaging infrastructure. It is initialized as part of the test setup
        /// and is intended to be used in derived test classes for sending test messages.
        /// </remarks>
        /// <value>
        /// An instance of <see cref="IAmAMessageProducerAsync"/> for sending messages asynchronously.
        /// </value>
        protected IAmAMessageProducerAsync MessageProducerAsync { get; }

        /// <summary>
        /// Gets the asynchronous message consumer used for receiving and managing messages
        /// in the MQTT messaging gateway during tests.
        /// </summary>
        /// <remarks>
        /// This property provides access to an implementation of <see cref="IAmAMessageConsumerAsync"/>,
        /// which facilitates operations such as receiving and purging messages from the messaging gateway.
        /// It is a core component for verifying message flow and behavior in test scenarios.
        /// </remarks>
        /// <value>
        /// An instance of <see cref="IAmAMessageConsumerAsync"/> representing the message consumer.
        /// </value>
        protected IAmAMessageConsumerAsync MessageConsumerAsync { get; }

        public void Dispose()
        {
            ((IAmAMessageProducerSync)MessageProducerAsync).Dispose();
            ((IAmAMessageConsumerSync)MessageConsumerAsync).Dispose();
            _mqttTestServer?.Dispose();
        }
    }
}
