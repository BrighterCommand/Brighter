using System;
using System.IO;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.Claims.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Claims.InMemory;
public class LargeMessagePaylodUnwrapTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly InMemoryStorageProvider _inMemoryStorageProvider;
    public LargeMessagePaylodUnwrapTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()), null);
        mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();
        _inMemoryStorageProvider = new InMemoryStorageProvider();
        var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_inMemoryStorageProvider, _inMemoryStorageProvider));
        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Test]
    public async Task When_unwrapping_a_large_message()
    {
        //arrange
        //store our luggage and get the claim check
        var contents = DataGenerator.CreateString(6000);
        var myCommand = new MyLargeCommand(1)
        {
            Value = contents
        };
        var commandAsJson = JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General));
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(commandAsJson);
        await writer.FlushAsync();
        stream.Position = 0;
        var id = await _inMemoryStorageProvider.StoreAsync(stream);
        //pretend we ran through the claim check
        myCommand.Value = $"Claim Check {id}";
        //set the headers, so that we have a claim check listed
        var message = new Message(new MessageHeader(myCommand.Id, new("transform.event"), MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow), new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General))));
        message.Header.DataRef = id;
        //act
        var transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyLargeCommand>();
        var transformedMessage = transformPipeline.Unwrap(message, new RequestContext());
        //assert
        //contents should be from storage
        await Assert.That(transformedMessage.Value).IsEqualTo(contents);
        await Assert.That(await _inMemoryStorageProvider.HasClaimAsync(id)).IsFalse();
    }
}