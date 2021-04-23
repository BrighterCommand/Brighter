using System.Text.Json.Serialization;

namespace Paramore.Brighter.ServiceActivator.Ports.Mappers
{
    public class HeartBeatResponseBody
    {
        [JsonPropertyName("hostName")]
        public string HostName { get;}
        [JsonPropertyName("consumers")]
        public HeartBeatResponseBodyConsumerObject[] Consumers { get;}

        public HeartBeatResponseBody(string hostName, HeartBeatResponseBodyConsumerObject[] consumers)
        {
            HostName = hostName;
            Consumers = consumers;
        }
    }
}
