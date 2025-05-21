using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class AsyncClaimCheckSmallPayloadTests
{
    private readonly ClaimCheckTransformer _transformerAsync;
    private readonly Message _message;
    private readonly RoutingKey _topic = new("test_topic");

    public AsyncClaimCheckSmallPayloadTests()
    {
        //arrange
        InMemoryStorageProvider store = new();
        InMemoryStorageProviderAsync storeAsync = new();
        _transformerAsync = new ClaimCheckTransformer(store, storeAsync);

        //set the threshold to 5K
        _transformerAsync.InitializeWrapFromAttributeParams(5);

        //but create a string that is just under 5K long - assuming string is 26 + length *2 to allow for 64-bit platform
        string body = DataGenerator.CreateString(2485);
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody(body));
    }

    [Fact]
    public async Task When_a_message_is_under_the_threshold()
    {
        var luggageCheckedMessage = await _transformerAsync.WrapAsync(_message, new Publication{Topic = new RoutingKey(_topic)});

        //assert
        bool hasLuggage = luggageCheckedMessage.Header.Bag.TryGetValue(ClaimCheckTransformer.CLAIM_CHECK, out object _);

        Assert.False(hasLuggage);
    }
}
