namespace Paramore.Brighter.MessagingGateway.MQTT
{
    public class MQTTMessagingGatewayConfiguration : IAmGatewayConfiguration
    {
        /// <summary>
        /// Sets the MQTT ClientID.
        /// </summary>
        public string ClientID { get; set; }
        
        /// <summary>
        /// Sets the MQTT Username
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Sets the MQTT Password
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Sets the CleanSession flag
        /// </summary>
        public bool CleanSession { get; set; }

        /// <summary>
        /// Sets the Server Hostname
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Sets the Topic Prefix
        /// </summary>
        public object TopicPrefix { get; set; }

        /// <summary>
        /// Sets the Connection Attempts
        /// </summary>
        public int ConnectionAttempts { get; internal set; } = 1;
    }


    public class MQTTMessagingGatewayProducerConfiguration : MQTTMessagingGatewayConfiguration{}
    public class MQTTMessagingGatewayConsumerConfiguration : MQTTMessagingGatewayConfiguration{}
}
