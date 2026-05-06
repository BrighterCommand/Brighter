using System;
using System.IO;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Claims.InMemory;
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
        _transformer = new ClaimCheckTransformer(_store, _store);
        _transformer.InitializeWrapFromAttributeParams(5);
        _body = DataGenerator.CreateString(6000);
        _message = new Message(new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow), new MessageBody(_body));
    }

    [Test]
    public async Task When_a_message_wraps_a_large_payload()
    {
        //act
        var luggageCheckedMessage = await _transformer.WrapAsync(_message, new Publication { Topic = new RoutingKey(_topic) });
        //assert
        bool hasLuggage = !string.IsNullOrEmpty(luggageCheckedMessage.Header.DataRef);
        await Assert.That(hasLuggage).IsTrue();
        var claimCheck = luggageCheckedMessage.Header.DataRef;
        var luggage = await new StreamReader(await _store.RetrieveAsync(claimCheck)).ReadToEndAsync();
        await Assert.That(luggage).IsEqualTo(_body);
    }
}