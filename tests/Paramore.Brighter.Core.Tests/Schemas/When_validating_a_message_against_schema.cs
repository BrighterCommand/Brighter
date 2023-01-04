using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NJsonSchema;
using NJsonSchema.Generation;
using Paramore.Brighter.Core.Tests.Schemas.Test_Doubles;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Schemas;

public class SchemaRegistryTransformValidationTests
{
    private readonly IAmAMessageTransformAsync _transformer;
    private readonly InMemorySchemaRegistry _schemaRegistry;
    private readonly string _topic = "io.goparamore.brighter.myschemregistrycommand";
    private readonly JsonSchema _schema;

    public SchemaRegistryTransformValidationTests()
    {
        _schemaRegistry = new InMemorySchemaRegistry();
        
        _transformer = new SchemaRegistryTransformer(_schemaRegistry, JsonSchemaGenerationSettings.Default);
        _transformer.InitializeWrapFromAttributeParams(typeof(MySchemaRegistryCommand), 1, true) ;
        
        var generator = new JsonSchemaGenerator(JsonSchemaGenerationSettings.Default);
        _schema = generator.Generate(typeof(MySchemaRegistryCommand));
    }
    
    [Fact]
    public async Task When_validating_a_good_message_against_schema()
    {
        //identify that we want to use version 2
        
        var command = new MySchemaRegistryCommand
        {
            IAmABool = false,
            IAmADouble = 20.0D,
            IAmAFloat = 19.0F,
            IAmAnInt = 5,
            IAmAString = "my command value",
            IAmAnotherString = "Another String",
            IAmAContainedType = new MyContainedType
            {
                IAmYetAnotherString = "Yet another string"
            } 
        };

        //register the schema ahead of validation; this will be version 1
        await _schemaRegistry.RegisterAsync(_topic, _schema.ToJson());

        var messageBody = JsonSerializer.Serialize<MySchemaRegistryCommand>(command, JsonSerialisationOptions.Options);
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(messageBody, "application/json")
        );
        
        //act
        try
        {
            var wrappedMessage = await _transformer.WrapAsync(message, new CancellationToken());

        }
        catch (Exception e)
        {
            Assert.Fail($"This is a good message, so no exception should be thrown: {e.Message}");
        }       
    }
    
    [Fact]
    public async Task When_validating_a_bad_message_against_schema()
    {
        var command = new MySchemaRegistryCommand
        {
            IAmABool = false,
            IAmADouble = 20.0D,
            IAmAFloat = 19.0F,
            IAmAnInt = 5,
            /* IAmAString = "my command value", Required but missing*/
            IAmAnotherString = "Another String",
            IAmAContainedType = new MyContainedType
            {
                IAmYetAnotherString = "Yet another string"
            } 
        };

        //register the schema ahead of validation; this will be version 1
        await _schemaRegistry.RegisterAsync(_topic, _schema.ToJson(), 1);

        var messageBody = JsonSerializer.Serialize<MySchemaRegistryCommand>(command, JsonSerialisationOptions.Options);
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(messageBody, "application/json")
        );
        
        //act
        await Assert.ThrowsAsync<InvalidSchemaException>(   () =>   _transformer.WrapAsync(message, new CancellationToken()));

    }
}
