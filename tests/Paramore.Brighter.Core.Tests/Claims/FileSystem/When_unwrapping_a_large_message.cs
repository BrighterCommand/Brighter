using System;
using System.IO;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.Claims.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Claims.FileSystem;
public class LargeMessagePayloadUnwrapTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly string _bucketName;
    private readonly FileSystemStorageProvider _luggageStore;
    public LargeMessagePayloadUnwrapTests()
    {
        //arrange
        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()), null);
        mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();
        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        _luggageStore = new FileSystemStorageProvider(new FileSystemOptions($"./{_bucketName}"));
        _luggageStore.EnsureStoreExists();
        var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));
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
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(commandAsJson);
        await writer.FlushAsync();
        stream.Position = 0;
        var id = await _luggageStore.StoreAsync(stream);
        //pretend we ran through the claim check
        myCommand.Value = $"Claim Check {id}";
        //set the headers, so that we have a claim check listed
        var message = new Message(new MessageHeader(myCommand.Id, new RoutingKey("MyLargeCommand"), MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow), new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General))));
        message.Header.DataRef = id;
        message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK] = id;
        //act
        var transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyLargeCommand>();
        var transformedMessage = transformPipeline.Unwrap(message, new RequestContext());
        //assert
        //contents should be from storage
        await Assert.That(transformedMessage.Value).IsEqualTo(contents);
        await Assert.That(await _luggageStore.HasClaimAsync(id)).IsFalse();
    }

    [After(Test)]
    public void Dispose()
    {
        //The bucket should be empty, allowing us to delete it
        Directory.Delete($"./{_bucketName}", true);
    }
}