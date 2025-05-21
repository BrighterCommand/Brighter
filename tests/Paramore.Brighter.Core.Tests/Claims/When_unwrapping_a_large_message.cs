using System;
using System.IO;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.Claims.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class LargeMessagePaylodUnwrapTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly InMemoryStorageProvider _inMemoryStorageProvider;

    public LargeMessagePaylodUnwrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()),
            null);
        mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();

        _inMemoryStorageProvider = new InMemoryStorageProvider();
        var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_inMemoryStorageProvider, new InMemoryStorageProviderAsync()));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Fact]
    public void When_unwrapping_a_large_message()
    {
        //arrange
        //store our luggage and get the claim check
        var contents = DataGenerator.CreateString(6000);
        var myCommand = new MyLargeCommand(1) { Value = contents };
        var commandAsJson = JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General));

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(commandAsJson);
        writer.Flush();
        stream.Position = 0;
        var id = _inMemoryStorageProvider.Store(stream);

        //pretend we ran through the claim check
        myCommand.Value = $"Claim Check {id}";
 
        //set the headers, so that we have a claim check listed
        var message = new Message(
            new MessageHeader(myCommand.Id, new("transform.event"), MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        message.Header.DataRef = id;

        //act
        var transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyLargeCommand>();
        var transformedMessage = transformPipeline.Unwrap(message, new RequestContext());

        //assert
        //contents should be from storage
        Assert.Equal(contents, transformedMessage.Value);
        Assert.False(_inMemoryStorageProvider.HasClaim(id));
    }
}
