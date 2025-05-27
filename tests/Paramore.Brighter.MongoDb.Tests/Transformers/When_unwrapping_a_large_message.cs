using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MongoDb.Tests.Helpers;
using Paramore.Brighter.MongoDb.Tests.TestDoubles;
using Paramore.Brighter.Transformers.MongoGridFS;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Transformers;

[Trait("Category", "MongoDb")]
public class LargeMessagePayloadUnwrapTests
{
    private readonly TransformPipelineBuilder _pipelineBuilder;
    private readonly MongoDbLuggageStore _luggageStore;

    public LargeMessagePayloadUnwrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()),
            null
        );

        mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();

        string bucketName = $"brightertestbucket-{Guid.NewGuid()}";

        _luggageStore = new MongoDbLuggageStore(new MongoDbLuggageStoreOptions(Configuration.ConnectionString, Configuration.DatabaseName, bucketName));
        _luggageStore.EnsureStoreExists();

        var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Fact]
    public void When_unwrapping_a_large_message()
    {
        //store our luggage and get the claim check
        var contents = DataGenerator.CreateString(6000);
        var myCommand = new MyLargeCommand(1) { Value = contents };
        var commandAsJson =
            JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General));

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(commandAsJson);
        writer.Flush();
        stream.Position = 0;
        var id = _luggageStore.Store(stream);

        //pretend we ran through the claim check
        myCommand.Value = $"Claim Check {id}";

        //set the headers, so that we have a claim check listed
        var message = new Message(
            new MessageHeader(myCommand.Id, new RoutingKey("MyLargeCommand"), MessageType.MT_COMMAND,
                timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(myCommand,
                new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        message.Header.DataRef = id;
        message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK] = id;

        //act
        var transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyLargeCommand>();
        var transformedMessage = transformPipeline.Unwrap(message, new RequestContext());

        //assert
        //contents should be from storage
        Assert.Equal(contents, transformedMessage.Value);
        Assert.False(_luggageStore.HasClaim(id));
    }
}
