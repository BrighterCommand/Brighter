namespace Paramore.Brighter.MessagingGateway.MQTT
{
    public class MqttMessagingGatewayConfiguration : IAmGatewayConfiguration
    {
        /// <summary>
        /// Sets the MQTT ClientID.
        /// </summary>
        public string? ClientID { get; set; }
        
        /// <summary>
        /// Sets the MQTT Username
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Sets the MQTT Password
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Sets the CleanSession flag
        /// </summary>
        public bool CleanSession { get; set; }

        /// <summary>
        /// Sets the Server Hostname
        /// </summary>
        public string? Hostname { get; set; }
        
        /// <summary>
        /// Gets or sets the port number used to connect to the MQTT broker.
        /// </summary>
        /// <value>
        /// The port number for the MQTT connection. The default value is 1883.
        /// </value>
        public int Port { get; set; } = 1883;

        /// <summary>
        /// Sets the Topic Prefix
        /// </summary>
        public object? TopicPrefix { get; set; }

        /// <summary>
        /// Sets the Connection Attempts
        /// </summary>
        public int ConnectionAttempts { get; internal set; } = 1;
    }


    public class MqttMessagingGatewayProducerConfiguration : MqttMessagingGatewayConfiguration;
    public class MqttMessagingGatewayConsumerConfiguration : MqttMessagingGatewayConfiguration;
}
