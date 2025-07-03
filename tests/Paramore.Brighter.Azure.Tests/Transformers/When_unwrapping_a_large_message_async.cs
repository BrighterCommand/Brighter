using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using Paramore.Brighter.Azure.Tests.Helpers;
using Paramore.Brighter.Azure.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transformers.Azure;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Azure.Tests.Transformers;

[Category("Azure")]
[Property("Fragile", "CI")]
public class LargeMessagePayloadAUnwrapAsyncTests : IAsyncDisposable 
{
    private readonly BlobContainerClient _client;
    private readonly AzureBlobLuggageStore _luggageStore;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;

    public LargeMessagePayloadAUnwrapAsyncTests()
    {
        //arrange
        var bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        var bucketUrl = new Uri($"{TestHelper.BlobLocation}{bucketName}");

        _client = new BlobContainerClient(bucketUrl, new AzureCliCredential());
        _client.CreateIfNotExists();
        _luggageStore = new AzureBlobLuggageStore(new AzureBlobLuggageOptions
        {
            ContainerUri = bucketUrl,
            Credential = new AzureCliCredential()
        });
        
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()),
            null);
        mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();
        
        var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, InstrumentationOptions.All);
    }
    
    [Test]
    public async Task When_unwrapping_a_large_message_async()
    {
        //arrange
        await Task.Delay(3000); //allow bucket definition to propagate
            
        //store our luggage and get the claim check
        var contents = DataGenerator.CreateString(6000);
        var myCommand = new MyLargeCommand(1) { Value = contents };
        var commandAsJson = JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General));
        
        var stream = new MemoryStream();                                                                               
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(commandAsJson);
        await writer.FlushAsync();
        stream.Position = 0;
        var id = await _luggageStore.StoreAsync(stream, CancellationToken.None);

        //pretend we ran through the claim check
        myCommand.Value = $"Claim Check {id}";
 
        //set the headers, so that we have a claim check listed
        var message = new Message(
            new MessageHeader(myCommand.Id, new RoutingKey("transform.event"), MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        message.Header.DataRef = id;
        message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK] = id; 
         
        //act
        var transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyLargeCommand>();
        var transformedMessage = await transformPipeline.UnwrapAsync(message, new RequestContext());
        
        //assert
        //contents should be from storage
        Assert.Equals(contents, transformedMessage.Value);
        Assert.That((await _luggageStore.HasClaimAsync(id, CancellationToken.None)));
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DeleteAsync();
    }
}
