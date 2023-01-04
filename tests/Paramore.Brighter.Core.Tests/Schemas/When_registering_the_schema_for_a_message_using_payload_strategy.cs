using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NJsonSchema;
using NJsonSchema.Generation;
using Paramore.Brighter.Core.Tests.Schemas.Test_Doubles;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Schemas;

public class SchemaRegistryTransformRegistrationPayloadTests 
{
    private readonly IAmAMessageTransformAsync _transformer;
    private readonly InMemorySchemaRegistry _schemaRegistry;
    private readonly string _topic = "io.goparamore.brighter.myschemregistrycommand";
    private readonly JsonSchema _schema;

    public SchemaRegistryTransformRegistrationPayloadTests()
    {
        _schemaRegistry = new InMemorySchemaRegistry();
        
        _transformer = new SchemaRegistryTransformer(_schemaRegistry, JsonSchemaGenerationSettings.Default);
        _transformer.InitializeWrapFromAttributeParams(
            typeof(MySchemaRegistryCommand), 
            1, 
            false,
            SchemaIdStrategy.Payload
            );
        
        var generator = new JsonSchemaGenerator(JsonSchemaGenerationSettings.Default);
        _schema = generator.Generate(typeof(MySchemaRegistryCommand));
    }

    [Fact]
    public async Task When_registering_the_schema_for_a_message()
    {
        //arrange
        
        _schemaRegistry.ClearSchemas();
        
        var command = new MySchemaRegistryCommand
        {
            IAmABool = false,
            IAmADouble = 20.0D,
            IAmAFloat = 19.0F,
            IAmAnInt = 5,
            IAmAString = "my command value",
            IAmAnotherString = "My command secret value",
            IAmAContainedType = new MyContainedType
            {
                IAmYetAnotherString = "Yet another string"
            }
        };

        var messageBody = JsonSerializer.Serialize<MySchemaRegistryCommand>(command, JsonSerialisationOptions.Options);
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(messageBody, "application/json")
        );
        
        var (exists, _) = await _schemaRegistry.LookupAsync(_topic);
        exists.Should().BeFalse();
        
        //act
        var wrappedMessage = await _transformer.WrapAsync(message, new CancellationToken());
        
        var (found, registeredSchema) = await _schemaRegistry.LookupAsync(_topic);

        //assert
        found.Should().BeTrue();
        BrighterMessageSchema messageSchema = registeredSchema.First();
        messageSchema.Schema.Should().Be(_schema.ToJson());
        messageSchema.Version.Should().Be(1);
        messageSchema.Subject.Should().Be(_topic);
        messageSchema.Id.Should().Be(1);
        wrappedMessage.Body.Bytes[0].Should().Be((byte)0);
        var schemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(wrappedMessage.Body.Bytes.Skip(1).Take(4).ToArray()));
        schemaId.Should().Be(messageSchema.Id);


    }

    [Fact]
    public async Task When_the_schema_is_already_registered()
    {
        _schemaRegistry.ClearSchemas();
        
         var command = new MySchemaRegistryCommand
        {
            IAmABool = false,
            IAmADouble = 20.0D,
            IAmAFloat = 19.0F,
            IAmAnInt = 5,
            IAmAString = "my command value",
            IAmAnotherString = "My command secret value",
            IAmAContainedType = new MyContainedType
            {
                IAmYetAnotherString = "Yet another string"
            } 
        };

        await _schemaRegistry.RegisterAsync(_topic, _schema.ToJson());

        var messageBody = JsonSerializer.Serialize<MySchemaRegistryCommand>(command, JsonSerialisationOptions.Options);
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(messageBody, "application/json")
        );
        
        //act
        var wrappedMessage = await _transformer.WrapAsync(message, new CancellationToken());
        
        var (found, registeredSchema) = await _schemaRegistry.LookupAsync(_topic);

        //assert
        found.Should().BeTrue();
        registeredSchema.Count().Should().Be(1);                        //we should not need to add a new schema
        BrighterMessageSchema messageSchema = registeredSchema.First();
        messageSchema.Schema.Should().Be(_schema.ToJson());
        messageSchema.Version.Should().Be(1);
        messageSchema.Subject.Should().Be(_topic);
        messageSchema.Id.Should().Be(1);
        var schemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(wrappedMessage.Body.Bytes.Skip(1).Take(4).ToArray()));
        schemaId.Should().Be(messageSchema.Id);

    }
    
    [Fact]
    public async Task When_an_earlier_version_is_already_registered()
    {
        _schemaRegistry.ClearSchemas();
         
        //identify that we want to use version 2
        _transformer.InitializeWrapFromAttributeParams(typeof(MySchemaRegistryCommand), 2);
        
        var command = new MySchemaRegistryCommand
        {
            IAmABool = false,
            IAmADouble = 20.0D,
            IAmAFloat = 19.0F,
            IAmAnInt = 5,
            IAmAString = "my command value",
            IAmAnotherString = "My command secret value",
            IAmAContainedType = new MyContainedType
            {
                IAmYetAnotherString = "Yet another string"
            } 
        };

        //this will be version 1
        await _schemaRegistry.RegisterAsync(_topic, _schema.ToJson(), 1);

        var messageBody = JsonSerializer.Serialize<MySchemaRegistryCommand>(command, JsonSerialisationOptions.Options);
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(messageBody, "application/json")
        );
        
        //act
        var wrappedMessage = await _transformer.WrapAsync(message, new CancellationToken());
        
        var (found, registeredSchema) = await _schemaRegistry.LookupAsync(_topic, false);

        //assert
        found.Should().BeTrue();
        registeredSchema.Count().Should().Be(2);                        //we should not need to add a new schema
        BrighterMessageSchema messageSchema = registeredSchema.Skip(1).Take(1).First();
        messageSchema.Schema.Should().Be(_schema.ToJson());
        messageSchema.Version.Should().Be(2);
        messageSchema.Subject.Should().Be(_topic);
        messageSchema.Id.Should().Be(1);
        var schemaId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(wrappedMessage.Body.Bytes.Skip(1).Take(4).ToArray()));
        schemaId.Should().Be(messageSchema.Id);
    }
}
