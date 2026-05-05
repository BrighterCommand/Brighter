using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class ClaimCheckWithoutStreamWriterTests
{
    private readonly InMemoryStorageProvider _storage = new();
    private readonly string _largeBody;
    private readonly RoutingKey _topic = new("test.topic");

    public ClaimCheckWithoutStreamWriterTests()
    {
        _largeBody = DataGenerator.CreateString(6000);
    }

    [Fact]
    public void When_wrapping_a_large_message_should_store_correctly()
    {
        // Arrange
        var transformer = new ClaimCheckTransformer(_storage, _storage);
        transformer.InitializeWrapFromAttributeParams(5);
        var message = CreateMessage(_largeBody);

        // Act
        var wrapped = transformer.Wrap(message, new Publication { Topic = _topic });

        // Assert — body replaced with claim check reference
        var id = wrapped.Header.DataRef;
        Assert.False(string.IsNullOrEmpty(id));
        Assert.Equal($"Claim Check {id}", wrapped.Body.Value);
        Assert.True(_storage.HasClaim(id));
    }

    [Fact]
    public void When_wrapping_and_unwrapping_should_round_trip()
    {
        // Arrange
        var wrapTransformer = new ClaimCheckTransformer(_storage, _storage);
        wrapTransformer.InitializeWrapFromAttributeParams(5);
        var message = CreateMessage(_largeBody);

        // Act — wrap
        var wrapped = wrapTransformer.Wrap(message, new Publication { Topic = _topic });

        // Act — unwrap
        var unwrapTransformer = new ClaimCheckTransformer(_storage, _storage);
        unwrapTransformer.InitializeUnwrapFromAttributeParams(true);
        var unwrapped = unwrapTransformer.Unwrap(wrapped);

        // Assert — round-trip recovers original body
        Assert.Equal(_largeBody, unwrapped.Body.Value);
    }

    [Fact]
    public async Task When_wrapping_and_unwrapping_async_should_round_trip()
    {
        // Arrange
        var wrapTransformer = new ClaimCheckTransformer(_storage, _storage);
        wrapTransformer.InitializeWrapFromAttributeParams(5);
        var message = CreateMessage(_largeBody);

        // Act — wrap
        var wrapped = await wrapTransformer.WrapAsync(message, new Publication { Topic = _topic });

        // Act — unwrap
        var unwrapTransformer = new ClaimCheckTransformer(_storage, _storage);
        unwrapTransformer.InitializeUnwrapFromAttributeParams(true);
        var unwrapped = await unwrapTransformer.UnwrapAsync(wrapped);

        // Assert — round-trip recovers original body
        Assert.Equal(_largeBody, unwrapped.Body.Value);
    }

    private Message CreateMessage(string body) =>
        new(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody(body, new ContentType(MediaTypeNames.Application.Json), CharacterEncoding.UTF8));
}
