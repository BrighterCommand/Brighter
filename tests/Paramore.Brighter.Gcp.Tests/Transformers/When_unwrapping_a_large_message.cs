using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transformers.Gcp;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Gcp.Tests.Transformers;

[Trait("Category", "GCS")]
public class LargeMessagePaylodUnwrapTests : IDisposable 
{
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly string _bucketName;
    private readonly GcsLuggageOptions _luggageStoreOptions;
    private readonly GcsLuggageStore _luggageStore;

    public LargeMessagePaylodUnwrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyLargeCommandMessageMapperAsync())
        );

        mapperRegistry.RegisterAsync<MyLargeCommand, MyLargeCommandMessageMapperAsync>();

        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";

        _luggageStoreOptions = new GcsLuggageOptions
        {
            ProjectId = GatewayFactory.GetProjectId(),
            Credential = GatewayFactory.GetCredential(),
            BucketName = _bucketName
        };
        
        _luggageStore = new GcsLuggageStore(_luggageStoreOptions);
            
        _luggageStore.EnsureStoreExists();

        var messageTransformerFactory =
            new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, InstrumentationOptions.None);
    }

    [Fact]
    public async Task When_unwrapping_a_large_message()
    {
        //arrange
        await Task.Delay(3000); //allow bucket definition to propagate

        //store our luggage and get the claim check
        var contents = DataGenerator.CreateString(6000);
        var myCommand = new MyLargeCommand(1) { Value = contents };
        var commandAsJson =
            JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General));

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(commandAsJson);
        await writer.FlushAsync();
        stream.Position = 0;
        var id = await _luggageStore.StoreAsync(stream);

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
        var transformedMessage = await transformPipeline.UnwrapAsync(message, new RequestContext());

        //assert
        //contents should be from storage
        Assert.Equal(contents, transformedMessage.Value); 
        Assert.False(await _luggageStore.HasClaimAsync(id));
    }

    public void Dispose()
    {
        var client = _luggageStoreOptions.CreateStorageClient();
        client.DeleteBucket(_bucketName);
    }
}
