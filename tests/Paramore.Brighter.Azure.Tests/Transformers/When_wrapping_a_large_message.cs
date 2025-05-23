using Azure.Identity;
using Azure.Storage.Blobs;
using Paramore.Brighter.Azure.Tests.Helpers;
using Paramore.Brighter.Azure.Tests.TestDoubles;
using Paramore.Brighter.Transformers.Azure;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Azure.Tests.Transformers;

[Category("Azure")]
[Property("Fragile", "CI")]
public class LargeMessagePayloadWrapTests : IDisposable
{
    private WrapPipelineAsync<MyLargeCommand>? _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly Publication _publication;
    private readonly MyLargeCommand _myCommand;
    private readonly AzureBlobLuggageStore _luggageStore;
    private readonly BlobContainerClient _client;
    private string? _id;

    public LargeMessagePayloadWrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

            var mapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()),
                null);
            mapperRegistry.Register<MyLargeCommand, MyLargeCommandMessageMapper>();    
            
            _publication = new Publication{ Topic = new RoutingKey("transform.event") };

            _myCommand = new MyLargeCommand(6000);

            string bucketName = $"brightertestbucket-{Guid.NewGuid()}";
            Uri bucketUrl = new($"{TestHelper.BlobLocation}{bucketName}");

            _client = new BlobContainerClient(bucketUrl, new AzureCliCredential());

            _luggageStore = new AzureBlobLuggageStore(new AzureBlobLuggageOptions
            {
                    ContainerUri = bucketUrl,
                    Credential = new AzureCliCredential()
            });

            var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));

            _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory);

            _client.CreateIfNotExists();
    }
    
    [Test]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = _transformPipeline.WrapAsync(_myCommand, new RequestContext(), _publication).Result;

        //assert
        Assert.That(message.Header.DataRef, Is.Not.Null);
        Assert.That(message.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK));
        Assert.That(message.Header.DataRef, Is.EqualTo((string)message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK]));
        
        _id = (string)message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK];
        Assert.Equals($"Claim Check {_id}", message.Body.Value);

        Assert.That(await _luggageStore.HasClaimAsync(_id, CancellationToken.None));
    }
    
    public void Dispose()
    {
        _client.Delete();
    }
}
