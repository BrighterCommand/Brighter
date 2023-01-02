using System;
using System.Linq;
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

public class SchemaRegistryTransformRegistrationTests 
{
    private readonly IAmAMessageTransformAsync _transformer;
    private readonly InMemorySchemaRegistry _schemaRegistry;
    private readonly string _topic = "io.goparamore.brighter.myschemregistrycommand";
    private readonly JsonSchema _schema;

    public SchemaRegistryTransformRegistrationTests()
    {
        _schemaRegistry = new InMemorySchemaRegistry();
        
        _transformer = new SchemaRegistryTransformer(_schemaRegistry, JsonSchemaGenerationSettings.Default);
        _transformer.InitializeWrapFromAttributeParams(typeof(MySchemaRegistryCommand), 1);
        
        var generator = new JsonSchemaGenerator(JsonSchemaGenerationSettings.Default);
        _schema = generator.Generate(typeof(MySchemaRegistryCommand));
    }

    [Fact]
    public async Task When_registering_the_schema_for_a_message()
    {
        //arrange
        var command = new MySchemaRegistryCommand
        {
            IAmABool = false,
            IAmADouble = 20.0D,
            IAmAFloat = 19.0F,
            IAmAnInt = 5,
            IAmAString = "my command value",
            IAmAnotherString = "My command secret value"
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
        registeredSchema.First().Schema.Should().Be(_schema.ToJson());

    }

    [Fact]
    public async Task When_the_schema_is_already_registered()
    {
        var command = new MySchemaRegistryCommand
        {
            IAmABool = false,
            IAmADouble = 20.0D,
            IAmAFloat = 19.0F,
            IAmAnInt = 5,
            IAmAString = "my command value",
            IAmAnotherString = "My command secret value"
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
        registeredSchema.First().Schema.Should().Be(_schema.ToJson());

    }
    
    [Fact]
    public async Task When_an_earlier_version_is_already_registered()
    {
        //identify that we want to use version 2
        _transformer.InitializeWrapFromAttributeParams(typeof(MySchemaRegistryCommand), 2);
        
        var command = new MySchemaRegistryCommand
        {
            IAmABool = false,
            IAmADouble = 20.0D,
            IAmAFloat = 19.0F,
            IAmAnInt = 5,
            IAmAString = "my command value",
            IAmAnotherString = "My command secret value"
        };

        //this will be version 1
        await _schemaRegistry.RegisterAsync(_topic, _schema.ToJson());

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
        registeredSchema.Skip(1).Take(1).First().Schema.Should().Be(_schema.ToJson());

    }
}
