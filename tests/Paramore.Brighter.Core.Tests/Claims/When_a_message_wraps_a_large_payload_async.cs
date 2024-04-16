using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class AsyncClaimCheckLargePayloadTests 
{
    private readonly ClaimCheckTransformerAsync _transformerAsync;
    private readonly Message _message;
    private readonly string _body;
    private readonly InMemoryStorageProviderAsync _store;
    private string _topic;

    public AsyncClaimCheckLargePayloadTests()
    {
        //arrange
        _store = new InMemoryStorageProviderAsync();
        _transformerAsync = new ClaimCheckTransformerAsync(store: _store);
        _transformerAsync.InitializeWrapFromAttributeParams(5);

        _body = DataGenerator.CreateString(6000);
        _topic = "test_topic";
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, DateTime.UtcNow),
            new MessageBody(_body));
    }
    
    [Fact]
    public async Task When_a_message_wraps_a_large_payload()
    {
        //act
        var luggageCheckedMessage = await _transformerAsync.WrapAsync(_message, new Publication{Topic = new RoutingKey(_topic)});

        //assert
        bool hasLuggage = luggageCheckedMessage.Header.Bag.TryGetValue(ClaimCheckTransformerAsync.CLAIM_CHECK, out object storedData);

        hasLuggage.Should().BeTrue();

        var claimCheck = (string)storedData;

        var luggage = await new StreamReader(await _store.RetrieveAsync(claimCheck)).ReadToEndAsync(); 
        
        luggage.Should().Be(_body);
    }
}
