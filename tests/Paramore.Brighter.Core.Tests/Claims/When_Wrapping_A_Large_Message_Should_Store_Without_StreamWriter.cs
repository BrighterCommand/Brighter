using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;

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

    [Test]
    public async Task When_wrapping_a_large_message_should_store_correctly()
    {
        var transformer = new ClaimCheckTransformer(_storage, _storage);
        transformer.InitializeWrapFromAttributeParams(5);
        var message = CreateMessage(_largeBody);

        var wrapped = transformer.Wrap(message, new Publication { Topic = _topic });

        var id = wrapped.Header.DataRef;
        await Assert.That(string.IsNullOrEmpty(id)).IsFalse();
        await Assert.That(wrapped.Body.Value).IsEqualTo($"Claim Check {id}");
        await Assert.That(_storage.HasClaim(id)).IsTrue();
    }

    [Test]
    public async Task When_wrapping_and_unwrapping_should_round_trip()
    {
        var wrapTransformer = new ClaimCheckTransformer(_storage, _storage);
        wrapTransformer.InitializeWrapFromAttributeParams(5);
        var message = CreateMessage(_largeBody);

        var wrapped = wrapTransformer.Wrap(message, new Publication { Topic = _topic });

        var unwrapTransformer = new ClaimCheckTransformer(_storage, _storage);
        unwrapTransformer.InitializeUnwrapFromAttributeParams(true);
        var unwrapped = unwrapTransformer.Unwrap(wrapped);

        await Assert.That(unwrapped.Body.Value).IsEqualTo(_largeBody);
    }

    [Test]
    public async Task When_wrapping_and_unwrapping_async_should_round_trip()
    {
        var wrapTransformer = new ClaimCheckTransformer(_storage, _storage);
        wrapTransformer.InitializeWrapFromAttributeParams(5);
        var message = CreateMessage(_largeBody);

        var wrapped = await wrapTransformer.WrapAsync(message, new Publication { Topic = _topic });

        var unwrapTransformer = new ClaimCheckTransformer(_storage, _storage);
        unwrapTransformer.InitializeUnwrapFromAttributeParams(true);
        var unwrapped = await unwrapTransformer.UnwrapAsync(wrapped);

        await Assert.That(unwrapped.Body.Value).IsEqualTo(_largeBody);
    }

    private Message CreateMessage(string body) =>
        new(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody(body, new ContentType(MediaTypeNames.Application.Json), CharacterEncoding.UTF8));
}
