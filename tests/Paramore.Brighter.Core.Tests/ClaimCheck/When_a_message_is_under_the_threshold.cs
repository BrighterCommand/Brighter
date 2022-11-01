using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.ClaimCheck;

public class ClaimCheckSmallPayloadTests
{
    private readonly ClaimCheckTransformer _transformer;
    private readonly Message _message;
    private readonly string _body;
    private readonly InMemoryStorageProviderAsync _store;

    public ClaimCheckSmallPayloadTests()
    {
        //arrange
        _store = new InMemoryStorageProviderAsync();
        _transformer = new ClaimCheckTransformer(store: _store);
        
        //set the threshold to 5K
        _transformer.InitializeWrapFromAttributeParams(5);

        //but create a string that is just under 5K long - assuming string is 26 + length *2 to allow for 64-bit platform
        _body = DataGenerator.CreateString(2485);
        _message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),
            new MessageBody(_body));
    }

    [Fact]
    public async Task When_a_message_is_under_the_threshold()
    {
        var luggageCheckedMessage = await _transformer.Wrap(_message);

        //assert
        bool hasLuggage = luggageCheckedMessage.Header.Bag.TryGetValue(ClaimCheckTransformer.CLAIM_CHECK, out object _);

        hasLuggage.Should().BeFalse();
    }
}
