using System;
using System.IO;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims.InMemory;

public class AsyncClaimCheckLargePayloadTests
{
    private readonly ClaimCheckTransformer _transformerAsync;
    private readonly Message _message;
    private readonly string _body;
    private readonly InMemoryStorageProvider _store;
    private readonly RoutingKey _topic = new("test_topic");

    public AsyncClaimCheckLargePayloadTests()
    {
        //arrange
        _store = new InMemoryStorageProvider();
        _transformerAsync = new ClaimCheckTransformer(_store, _store);
        _transformerAsync.InitializeWrapFromAttributeParams(5);

        _body = DataGenerator.CreateString(6000);
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody(_body));
    }

    [Fact]
    public async Task When_a_message_wraps_a_large_payload()
    {
        //act
        var luggageCheckedMessage = await _transformerAsync.WrapAsync(_message, new Publication{Topic = new RoutingKey(_topic)});

        //assert
        bool hasLuggage = luggageCheckedMessage.Header.Bag.TryGetValue(ClaimCheckTransformer.CLAIM_CHECK, out var storedData);

        Assert.True(hasLuggage);
        Assert.Equal(luggageCheckedMessage.Header.DataRef, storedData);

        var claimCheck = (string)storedData!;

        var luggage = await new StreamReader(await _store.RetrieveAsync(claimCheck)).ReadToEndAsync();

        Assert.Equal(_body, luggage);
    }
}
