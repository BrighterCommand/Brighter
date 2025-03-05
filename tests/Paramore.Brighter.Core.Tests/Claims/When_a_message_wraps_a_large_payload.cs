using System;
using System.IO;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class ClaimCheckLargePayloadTests
{
    private readonly ClaimCheckTransformer _transformer;
    private readonly Message _message;
    private readonly string _body;
    private readonly InMemoryStorageProvider _store;
    private readonly RoutingKey _topic = new("test_topic");

    public ClaimCheckLargePayloadTests()
    {
        //arrange
        _store = new InMemoryStorageProvider();
        _transformer = new ClaimCheckTransformer(store: _store);
        _transformer.InitializeWrapFromAttributeParams(5);

        _body = DataGenerator.CreateString(6000);
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody(_body));

    }

    [Fact]
    public void When_a_message_wraps_a_large_payload()
    {
        //act
        var luggageCheckedMessage = _transformer.Wrap(_message, new Publication{Topic = new RoutingKey(_topic)});

        //assert
        bool hasLuggage = !string.IsNullOrEmpty(luggageCheckedMessage.Header.DataRef);

        Assert.True(hasLuggage);

        var claimCheck = luggageCheckedMessage.Header.DataRef;

        var luggage = new StreamReader(_store.Retrieve(claimCheck)).ReadToEnd();

        Assert.Equal(_body, luggage);
    }
}
