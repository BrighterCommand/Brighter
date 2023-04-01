using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using FluentAssertions;
using Paramore.Brighter.Azure.TestDoubles.Tests;
using Paramore.Brighter.Azure.Tests.Helpers;
using Paramore.Brighter.Azure.Tests.TestDoubles;
using Paramore.Brighter.Transformers.Azure;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Azure.Tests.Transformers;

[Category("Azure")]
[Property("Fragile", "CI")]
public class LargeMessagePaylodUnwrapTests : IDisposable
{
    private readonly BlobContainerClient _client;
    private readonly string _bucketName;
    private Uri _bucketUrl;
    private AzureBlobLuggageStore _luggageStore;
    private readonly TransformPipelineBuilder _pipelineBuilder;

    public LargeMessagePaylodUnwrapTests()
    {
        //arrange
        _bucketName = $"brightertestbucket-{Guid.NewGuid()}";
        _bucketUrl = new Uri($"{TestHelper.BlobLocation}{_bucketName}");

        _client = new BlobContainerClient(_bucketUrl, new AzureCliCredential());
        _client.CreateIfNotExists();
        _luggageStore = new AzureBlobLuggageStore(_bucketUrl, new AzureCliCredential());

        TransformPipelineBuilder.ClearPipelineCache();

        var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyLargeCommandMessageMapper()))
        {
            { typeof(MyLargeCommand), typeof(MyLargeCommandMessageMapper) }
        };

        var messageTransformerFactory = new SimpleMessageTransformerFactory(_ => new ClaimCheckTransformer(_luggageStore));

        _pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, messageTransformerFactory);
    }

    [Test]
    public async Task When_unwrapping_a_large_message()
    {
        //arrange
        await Task.Delay(3000); //allow bucket definition to propogate

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
            new MessageHeader(myCommand.Id, "transform.event", MessageType.MT_COMMAND, DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(myCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        );

        message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK] = id;

        //act
        var transformPipeline = _pipelineBuilder.BuildUnwrapPipeline<MyLargeCommand>();
        var transformedMessage = await transformPipeline.UnwrapAsync(message);

        //assert
        //contents should be from storage
        transformedMessage.Value.Should().Be(contents);
        (await _luggageStore.HasClaimAsync(id, CancellationToken.None)).Should().BeFalse();
    }

    public void Dispose()
    {
        _client.Delete();
    }
}
