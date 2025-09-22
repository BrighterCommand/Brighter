using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transformers.AWS.V4;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.Transformers;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class LargeMessagePaylodUnwrapTests : IAsyncDisposable 
{
    private readonly TransformPipelineBuilderAsync _pipelineBuilder;
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;
    private readonly S3LuggageStore _luggageStore;

    public LargeMessagePaylodUnwrapTests()
    {
        //arrange
        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyLargeCommandMessageMapperAsync())
        );

        mapperRegistry.RegisterAsync<MyLargeCommand, MyLargeCommandMessageMapperAsync>();

        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";

        _luggageStore = new S3LuggageStore(new S3LuggageOptions(GatewayFactory.CreateS3Connection(), _bucketName)
        {
            HttpClientFactory = httpClientFactory,
            BucketAddressTemplate = CredentialsChain.GetBucketAddressTemple(),
            ACLs = S3CannedACL.Private,
            Tags = [new Tag { Key = "BrighterTests", Value = "S3LuggageUploadTests" }]
        });
            
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
        Assert.False((await _luggageStore.HasClaimAsync(id)));
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DeleteBucketAsync(_bucketName);
        _client.Dispose();
    }
}
