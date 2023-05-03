using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using Paramore.Brighter;
using SalutationPorts.Requests;

namespace SalutationAnalytics.Mappers
{
    public class GreetingMadeMessageMapper : IAmAMessageMapper<GreetingMade>
    {
        private readonly SerializationContext _serializationContext;
        private const string Topic = "greeting.event";

        public GreetingMadeMessageMapper()
        {
            //We care about ensuring that we serialize the body using the Confluent tooling, as it registers and validates schema
            _serializationContext = new SerializationContext(MessageComponentType.Value, Topic);
        }
        public Message MapToMessage(GreetingMade request)
        {
            throw new System.NotImplementedException();
        }

        public GreetingMade MapToRequest(Message message)
        {
            var deserializer = new JsonDeserializer<GreetingMade>().AsSyncOverAsync();
            //This uses the Confluent JSON serializer, which wraps Newtonsoft but also performs schema registration and validation
             var greetingCommand = deserializer.Deserialize(message.Body.Bytes, message.Body.Bytes is null, _serializationContext);
            
            return greetingCommand;       
        }
    }
}
