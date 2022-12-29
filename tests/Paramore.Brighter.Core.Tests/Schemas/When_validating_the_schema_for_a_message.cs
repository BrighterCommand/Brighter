using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using NJsonSchema;
using NJsonSchema.Generation;
using Paramore.Brighter.Core.Tests.Schemas.Test_Doubles;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Schemas;

public class SchemaRegistryTransformerTests 
{
    private readonly IAmAMessageTransformAsync _transformer;
    private readonly InMemorySchemaRegistry _schemaRegistry;
    private readonly string _topic = "io.goparamore.brighter.myschemregistrycommand";
    private readonly JsonSchema _schema;

    public SchemaRegistryTransformerTests()
    {
        _schemaRegistry = new InMemorySchemaRegistry();
        
        _transformer = new SchemaRegistryTransformer(_schemaRegistry, JsonSchemaGenerationSettings.Default);
        _transformer.InitializeWrapFromAttributeParams(typeof(MySchemaRegistryCommand));
        
        var generator = new JsonSchemaGenerator(JsonSchemaGenerationSettings.Default);
        _schema = generator.Generate(typeof(MySchemaRegistryCommand));
    }

    [Fact]
    public async Task When_validating_the_schema_for_a_message()
    {
        //arrange
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
                IAmAAnotherString = "My contained value"
            }
        };

        var messageBody = JsonSerializer.Serialize<MySchemaRegistryCommand>(command, JsonSerialisationOptions.Options);
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(messageBody, "application/json")
        );
        
        //act
        var wrappedMessage = await _transformer.WrapAsync(message, default);
        var (found, registeredSchema) = await _schemaRegistry.LookupAsync(_topic);

        //assert
        found.Should().BeTrue();
        registeredSchema.Should().Be(_schema.ToJson());

    }
}
