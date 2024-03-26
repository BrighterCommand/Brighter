using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
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

    public ClaimCheckLargePayloadTests()
    {
        //arrange
        _store = new InMemoryStorageProvider();
        _transformer = new ClaimCheckTransformer(store: _store);
        _transformer.InitializeWrapFromAttributeParams(5);

        _body = DataGenerator.CreateString(6000);
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),
            new MessageBody(_body));
    }
    
    [Fact]
    public void When_a_message_wraps_a_large_payload()
    {
        //act
        var luggageCheckedMessage = _transformer.Wrap(_message);

        //assert
        bool hasLuggage = luggageCheckedMessage.Header.Bag.TryGetValue(ClaimCheckTransformer.CLAIM_CHECK, out object storedData);

        hasLuggage.Should().BeTrue();

        var claimCheck = (string)storedData;

        var luggage = new StreamReader(_store.Retrieve(claimCheck)).ReadToEnd(); 
        
        luggage.Should().Be(_body);
    }
}
