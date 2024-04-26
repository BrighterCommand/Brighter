using Azure.Identity;
using Azure.Storage.Blobs;
using FluentAssertions;
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

            _luggageStore = new AzureBlobLuggageStore(bucketUrl, new AzureCliCredential());

            var messageTransformerFactory = new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformerAsync(_luggageStore));

            _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, messageTransformerFactory, new InMemoryRequestContextFactory());

            _client.CreateIfNotExists();
    }
    
    [Test]
    public async Task When_wrapping_a_large_message()
    {
        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = _transformPipeline.WrapAsync(_myCommand, _publication).Result;

        //assert
        message.Header.Bag.ContainsKey(ClaimCheckTransformerAsync.CLAIM_CHECK).Should().BeTrue();
        _id = (string)message.Header.Bag[ClaimCheckTransformerAsync.CLAIM_CHECK];
        message.Body.Value.Should().Be($"Claim Check {_id}");
            
        (await _luggageStore.HasClaimAsync(_id, CancellationToken.None)).Should().BeTrue();
    }
    
    public void Dispose()
    {
        _client.Delete();
    }
}
