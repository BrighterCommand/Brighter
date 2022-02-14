using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    public class MQTTMessagingGatewayConfiguration : IAmGatewayConfiguration
    {
        public string ClientID { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool CleanSession { get; set; }
        public string Hostname { get; set; }
        public object TopicPrefix { get; set; }
        public int ConnectionAttempts { get; internal set; } = 1;
    }


    public class MQTTMessagingGatewayProducerConfiguration : MQTTMessagingGatewayConfiguration
    {

    }
    public class MQTTMessagingGatewayConsumerConfiguration : MQTTMessagingGatewayConfiguration
    {
        public Queue<Message> queue { get; set; }
    }
}
