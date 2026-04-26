using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MQTT;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Server;
using Paramore.Test.Helpers.Base;
using Paramore.Test.Helpers.Loggers;

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
    public abstract class MqttTestClassBase<T> : TestClassBase<T> 
        where T : class
    {
        protected static readonly MqttFactory s_mqttFactory = new();

        protected readonly Message _noopMessage = new();
        protected MqttTestServer? MqttTestServer;

        private readonly IPAddress _serverIPAddress;
        private readonly int _serverPort;
        private readonly string _uniqueClientId;
        private readonly string _uniqueTopicPrefix;

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttTestClassBase{T}"/> class with the specified client ID, topic prefix, and test output helper.
        /// </summary>
        /// <param name="clientID">The unique identifier for the MQTT client.</param>
        /// <param name="topicPrefix">The prefix for MQTT topics used in the test.</param>
        /// <param name="testOutputHelper">The output helper for capturing test output during execution.</param>
        /// <remarks>
        /// This constructor sets up the necessary MQTT test server and configurations for messaging gateway tests.
        /// It also configures logging to integrate with the test output helper.
        /// </remarks>
        protected MqttTestClassBase(string clientID, string topicPrefix)
        : base()
        {
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            _uniqueClientId = $"{clientID}-{uniqueSuffix}";
            _uniqueTopicPrefix = $"{topicPrefix}/{uniqueSuffix}";

            ApplicationLogging.LoggerFactory = LoggerFactory.Create(configure =>
            {
                configure.Services.AddSingleton(TestOutputHelper);
                configure.Services.AddSingleton<ITestOutputLoggingProvider, TestOutputLoggingProvider>();
                configure.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<ITestOutputLoggingProvider>());
                configure.Services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger), typeof(TestOutputLogger)));
                configure.Services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TestOutputLogger<>)));
            });

            _serverIPAddress = IPAddress.Any;
            _serverPort = Helpers.Server.MqttTestServer.GetRandomServerPort();
        }

        [Before(HookType.Test)]
        public virtual async Task SetupMqttServer()
        {
            // Server must be running before MqttMessagePublisher's constructor connects.
            MqttTestServer = await Helpers.Server.MqttTestServer.CreateTestMqttServer(
                s_mqttFactory, true, ApplicationLogging.CreateLogger<T>(), _serverIPAddress, _serverPort, null, TestDisplayName);

            var mqttProducerConfig = new MqttMessagingGatewayProducerConfiguration
            {
                Hostname = IPAddress.Loopback.ToString(),
                Port = _serverPort,
                TopicPrefix = _uniqueTopicPrefix
            };

            MqttMessagePublisher mqttMessagePublisher = new(mqttProducerConfig);
            MessageProducerAsync = new MqttMessageProducer(mqttMessagePublisher, new Publication());

            MqttMessagingGatewayConsumerConfiguration mqttConsumerConfig = new()
            {
                Hostname = IPAddress.Loopback.ToString(),
                Port = _serverPort,
                TopicPrefix = _uniqueTopicPrefix,
                ClientID = _uniqueClientId
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
        protected IAmAMessageProducerAsync MessageProducerAsync { get; private set; } = null!;

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
        protected IAmAMessageConsumerAsync MessageConsumerAsync { get; private set; } = null!;

        /// <summary>
        /// Releases the resources used by the <see cref="MqttTestClassBase{T}"/> instance.
        /// </summary>
        /// <param name="disposing">
        /// A boolean value indicating whether the method is being called explicitly 
        /// to release both managed and unmanaged resources (<c>true</c>), 
        /// or by the finalizer to release only unmanaged resources (<c>false</c>).
        /// </param>
        /// <remarks>
        /// This method ensures that all disposable components, such as the message producer, 
        /// message consumer, and MQTT test server, are properly disposed of when no longer needed.
        /// It also calls the base class's <see cref="TestClassBase.Dispose(bool)"/> method to 
        /// perform additional cleanup operations.
        /// </remarks>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                (MessageProducerAsync as IAmAMessageProducerSync)?.Dispose();
                (MessageConsumerAsync as IAmAMessageConsumerSync)?.Dispose();
                MqttTestServer?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
