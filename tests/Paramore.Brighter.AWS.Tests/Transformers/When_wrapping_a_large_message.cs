using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transformers.AWS;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.AWS.Tests.Transformers;

[Property("Category", "AWS")]
public class LargeMessagePayloadWrapTests
{
    private string? _id;
    private WrapPipelineAsync<MyLargeCommand>? _transformPipeline;
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly MyLargeCommand _myCommand;
    private readonly S3LuggageStore _luggageStore;
    private readonly string _bucketName;

    private readonly Publication _publication;

    public LargeMessagePayloadWrapTests()
    {
        //arrange

        var mapperRegistry =
            new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(
                _ => new MyLargeCommandMessageMapperAsync())
            );

        mapperRegistry.RegisterAsync<MyLargeCommand, MyLargeCommandMessageMapperAsync>();

        _myCommand = new MyLargeCommand(6000);

        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";

        _luggageStore = new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), _bucketName)
        {
            HttpClientFactory = httpClientFactory,
            BucketAddressTemplate = CredentialsChain.GetBucketAddressTemplate(),
            ACLs = S3CannedACL.Private,
            Tags = [new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" }],
        });

        var transformerFactoryAsync = new SimpleMessageTransformerFactoryAsync(_ => new ClaimCheckTransformer(_luggageStore, _luggageStore));

        _publication = new Publication { Topic = new RoutingKey("MyLargeCommand"), RequestType = typeof(MyLargeCommand) };

        _pipelineBuilder = new TransformPipelineBuilderAsync(mapperRegistry, transformerFactoryAsync, InstrumentationOptions.All);
    }

    [Test]
    public async Task When_wrapping_a_large_message()
    {
        await _luggageStore.EnsureStoreExistsAsync();

        //act
        _transformPipeline = _pipelineBuilder.BuildWrapPipeline<MyLargeCommand>();
        var message = await _transformPipeline.WrapAsync(_myCommand, new RequestContext(), _publication);

        //assert
        await Assert.That(message.Header.Bag.ContainsKey(ClaimCheckTransformer.CLAIM_CHECK)).IsTrue();
        await Assert.That(message.Header.DataRef).IsNotNull();
        _id = (string)message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK];
        await Assert.That(message.Body.Value).IsEqualTo($"Claim Check {_id}");

        await Assert.That(await _luggageStore.HasClaimAsync(_id)).IsTrue();
    }

    [Before(HookType.Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(HookType.Test)]
    public Task DisposeAsync() => S3TestBucketCleanup.DeleteAsync(_bucketName);
}
