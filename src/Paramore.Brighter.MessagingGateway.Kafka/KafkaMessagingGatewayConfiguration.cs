using System;
using System.Collections.Generic;
using System.Text;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public class KafkaMessagingGatewayConfiguration
    {
        public string Name { get; set; }

        public string[] BootStrapServers { get; set; }

        public IEnumerable<KeyValuePair<string, object>> ToConfig()
        {
            var config = new Dictionary<string, object>()
            {
                {"client.id", Name },
                {"bootstrap.servers", string.Join(";", BootStrapServers)}
            };
            return config;
        }
    }
}
