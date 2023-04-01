#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Net.Mime;
using Greetings.Ports.Commands;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Greetings.Ports.Mappers
{
    public class GreetingEventMessageMapper : IAmAMessageMapper<GreetingEvent>
    {
        private readonly ISchemaRegistryClient _schemaRegistryClient;
        private readonly string _partitionKey = "KafkaTestQueueExample_Partition_One";
        private SerializationContext _serializationContext;
        private const string Topic = "greeting.event";

        public GreetingEventMessageMapper(ISchemaRegistryClient schemaRegistryClient)
        {
            _schemaRegistryClient = schemaRegistryClient;
            //We care about ensuring that we serialize the body using the Confluent tooling, as it registers and validates schema
            _serializationContext = new SerializationContext(MessageComponentType.Value, Topic);
        }

        public Message MapToMessage(GreetingEvent request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: Topic, messageType: MessageType.MT_EVENT);
            //This uses the Confluent JSON serializer, which wraps Newtonsoft but also performs schema registration and validation
            var serializer = new JsonSerializer<GreetingEvent>(_schemaRegistryClient, ConfluentJsonSerializationConfig.SerdesJsonSerializerConfig(), ConfluentJsonSerializationConfig.NJsonSchemaGeneratorSettings()).AsSyncOverAsync();
            var s = serializer.Serialize(request, _serializationContext);
            var body = new MessageBody(s, MediaTypeNames.Application.Octet, CharacterEncoding.Raw);
            header.PartitionKey = _partitionKey;

            var message = new Message(header, body);
            return message;
        }

        public GreetingEvent MapToRequest(Message message)
        {
            var deserializer = new JsonDeserializer<GreetingEvent>().AsSyncOverAsync();
            //This uses the Confluent JSON serializer, which wraps Newtonsoft but also performs schema registration and validation
             var greetingCommand = deserializer.Deserialize(message.Body.Bytes, message.Body.Bytes is null, _serializationContext);

            return greetingCommand;
        }
    }
}
