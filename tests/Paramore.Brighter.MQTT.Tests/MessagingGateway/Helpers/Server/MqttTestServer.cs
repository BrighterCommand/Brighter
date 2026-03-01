using System;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Server;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Loggers;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Proactor;
using Shouldly;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Server
{
    public class MqttTestServer : IDisposable
    {
        protected MqttTestServer(MqttFactory mqttFactory, int serverPort, ILogger logger, MqttServerOptions mqttServerOptions, [CallerMemberName] string? testMethodName = null)
        {
            MqttFactory = mqttFactory ?? throw new ArgumentNullException(nameof(mqttFactory));
            MqttServerOptions = mqttServerOptions ?? throw new ArgumentNullException(nameof(mqttServerOptions));

            if (!string.IsNullOrEmpty(testMethodName))
            {
                logger.BeginScope("{TestName}", testMethodName!);
            }
        }

        public MqttFactory MqttFactory { get; }

        public MqttServer MqttServer { get; protected set; } = null!;

        public MqttServerOptions MqttServerOptions { get; }

        public IPAddress ServerIPAddress => MqttServerOptions.DefaultEndpointOptions.BoundInterNetworkAddress;

        public int ServerPort => MqttServerOptions.DefaultEndpointOptions.Port;

        public string HostName => MqttServerOptions.DefaultEndpointOptions.BoundInterNetworkAddress.ToString();

        public static MqttTestServer? CreateTestMqttServer(MqttFactory mqttFactory, bool startService = true, ILogger? logger = null, IPAddress? serverIPAddress = null, int? serverPort = null, MqttServerOptions? mqttServerOptions = null, [CallerMemberName] string? testMethodName = null)
        {
            ArgumentNullException.ThrowIfNull(mqttFactory);

            serverPort ??= GetRandomServerPort();

            serverIPAddress ??= IPAddress.Loopback;
            testMethodName ??= nameof(MqttTestServer);
            logger ??= CreateLogger(testMethodName);

            MqttTestServer? testMqttServer = null;

            try
            {
                mqttServerOptions ??= CreateDefaultMqttServerOptions(mqttFactory, serverIPAddress, serverPort.Value);
                logger.LogInformation("Creating MQTT Test Server on {IPAddress}:{Port}", mqttServerOptions.DefaultEndpointOptions.BoundInterNetworkAddress, serverPort.Value);

                testMqttServer = new MqttTestServer(mqttFactory, serverPort.Value, logger, mqttServerOptions, testMethodName)
                {
                    MqttServer = mqttFactory.CreateMqttServer(mqttServerOptions, new MqttTestLogger(logger)),
                };

                if (testMqttServer.MqttServer is null)
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }

            testMqttServer.ShouldNotBeNull();
            testMqttServer.ShouldBeOfType<MqttTestServer>();

            testMqttServer.MqttServer.IsStarted.ShouldBeFalse();

            try
            {
                if (startService)
                {
                    testMqttServer.MqttServer.StartAsync().GetAwaiter().GetResult();
                    testMqttServer.MqttServer.IsStarted.ShouldBeTrue();
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to start MqttServer");
                return null;
            }

            return testMqttServer;
        }


        public static int GetRandomServerPort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public static ILogger CreateLogger(string categoryName)
        {
            return NullLogger.Instance;
        }

        public static MqttServerOptions CreateDefaultMqttServerOptions(MqttFactory mqttFactory, IPAddress serverIPAddress, int serverPort)
        {
            return mqttFactory
                .CreateServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(serverIPAddress)
                .WithDefaultEndpointPort(serverPort)
                .Build();
        }

        public void Dispose()
        {
            MqttServer?.Dispose();
        }
    }
}
