using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.ClaimCheck;

public class ClaimCheckLargePayloadTests 
{
    private readonly ClaimCheckTransformer _transfomer;
    private readonly Message _message;
    private readonly string _body;
    private readonly InMemoryStorageProviderAsync _store;

    public ClaimCheckLargePayloadTests()
    {
        //arrange
        _store = new InMemoryStorageProviderAsync();
        _transfomer = new ClaimCheckTransformer(store: _store);
        _transfomer.InitializeWrapFromAttributeParams(5);

        _body = DataGenerator.CreateString(6000);
        _message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),
            new MessageBody(_body));
    }
    
    [Fact]
    public async Task When_a_message_wraps_a_large_payload()
    {
        //act
        var luggageCheckedMessage = await _transfomer.Wrap(_message);

        //assert
        bool hasLuggage = luggageCheckedMessage.Header.Bag.TryGetValue(ClaimCheckTransformer.CLAIM_CHECK, out object storedData);

        hasLuggage.Should().BeTrue();

        var claimCheck = (Guid)storedData;

        var luggage = await new StreamReader(await _store.DownloadAsync(claimCheck)).ReadToEndAsync(); 
        
        luggage.Should().Be(_body);
    }



}
