using System;
using Microsoft.Extensions.Logging;
using MQTTnet.Diagnostics;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Loggers
{
    /// <summary>
    /// Provides a test implementation of the <see cref="IMqttNetLogger"/> interface, 
    /// allowing logging of MQTT messages using the <see cref="ILogger"/> abstraction.
    /// </summary>
    /// <remarks>
    /// This class is designed to integrate MQTT logging with the Microsoft.Extensions.Logging framework,
    /// enabling structured and configurable logging for MQTT-related operations during testing.
    /// </remarks>
    public class MqttTestLogger : IMqttNetLogger
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MqttTestLogger"/> class with the specified wrappedLogger.
        /// </summary>
        /// <param name="wrappedLogger">
        /// An instance of <see cref="ILogger"/> used to log MQTT messages.
        /// </param>
        /// <remarks>
        /// This constructor allows the integration of MQTT logging with the Microsoft.Extensions.Logging framework,
        /// enabling structured and configurable logging for testing purposes.
        /// </remarks>
        public MqttTestLogger(ILogger wrappedLogger)
        {
            WrappedLogger = wrappedLogger;
        }

        /// <summary>
        /// Gets or sets the <see cref="ILogger"/> instance used for logging MQTT messages.
        /// </summary>
        /// <value>
        /// An instance of <see cref="ILogger"/> that facilitates structured and configurable logging
        /// for MQTT-related operations during testing.
        /// </value>
        /// <remarks>
        /// This property allows the integration of MQTT logging with the Microsoft.Extensions.Logging framework,
        /// enabling detailed logging for debugging and analysis in test scenarios.
        /// </remarks>
        public ILogger WrappedLogger { get; set; }

        /// <inheritdoc />
        public bool IsEnabled => !WrappedLogger.IsEnabled(LogLevel.None);

        /// <inheritdoc />
        public void Publish(MqttNetLogLevel mqttNetlogLevel, string source, string message, object[] parameters,
            Exception exception)
        {
            LogLevel logLevel = ConvertLogLevel(mqttNetlogLevel);

            if (!this.WrappedLogger.IsEnabled(logLevel))
            {
                return;
            }

            WrappedLogger.Log(logLevel, exception, message, parameters);
        }

        public static LogLevel ConvertLogLevel(MqttNetLogLevel logLevel)
        {
            return logLevel switch
            {
                MqttNetLogLevel.Verbose => LogLevel.Trace,
                MqttNetLogLevel.Info => LogLevel.Information,
                MqttNetLogLevel.Warning => LogLevel.Warning,
                MqttNetLogLevel.Error => LogLevel.Error,
                _ => LogLevel.None
            };
        }
    }
}
