using System.Net.Mime;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;


namespace GreetingsWeb.Mappers
{
    public class GreetingMadeMessageMapper : IAmAMessageMapper<GreetingMade>
    {
        private readonly ISchemaRegistryClient _schemaRegistryClient;
        private readonly string _partitionKey = "KafkaTestQueueExample_Partition_One";
        private SerializationContext _serializationContext;
        private const string Topic = "greeting.event";
        public GreetingMadeMessageMapper(ISchemaRegistryClient schemaRegistryClient)
        {
            _schemaRegistryClient = schemaRegistryClient;
            //We care about ensuring that we serialize the body using the Confluent tooling, as it registers and validates schema
            _serializationContext = new SerializationContext(MessageComponentType.Value, Topic);
        }
 
        public Message MapToMessage(GreetingMade request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: Topic, messageType: MessageType.MT_EVENT);
            //This uses the Confluent JSON serializer, which wraps Newtonsoft but also performs schema registration and validation
            var serializer = new JsonSerializer<GreetingMade>(_schemaRegistryClient, ConfluentJsonSerializationConfig.SerdesJsonSerializerConfig(), ConfluentJsonSerializationConfig.NJsonSchemaGeneratorSettings()).AsSyncOverAsync();
            var s = serializer.Serialize(request, _serializationContext);
            var body = new MessageBody(s, MediaTypeNames.Application.Octet, CharacterEncoding.Raw);
            header.PartitionKey = _partitionKey;

            var message = new Message(header, body);
            return message;
 
        }

        public GreetingMade MapToRequest(Message message)
        {
            throw new System.NotImplementedException();
        }
    }
}
