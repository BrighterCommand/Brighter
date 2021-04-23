using System.Text.Json.Serialization;

namespace Paramore.Brighter.ServiceActivator.Ports.Mappers
{
    public class HeartBeatResponseBodyConsumerObject
    {
        [JsonPropertyName("consumerName")]
        public string ConsumerName { get; }

        [JsonPropertyName("state")]
        public ConsumerState State { get; }

        public HeartBeatResponseBodyConsumerObject(string consumerName, ConsumerState state)
        {
            ConsumerName = consumerName;
            State = state;
        }
    }
}
