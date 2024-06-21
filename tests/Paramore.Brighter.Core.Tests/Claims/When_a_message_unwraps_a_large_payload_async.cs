using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class AsyncRetrieveClaimLargePayloadTests
{
    private readonly InMemoryStorageProviderAsync _store;
    private readonly ClaimCheckTransformerAsync _transformerAsync;
    private readonly string _contents;

    public AsyncRetrieveClaimLargePayloadTests()
    {
        _store = new InMemoryStorageProviderAsync();
        _transformerAsync = new ClaimCheckTransformerAsync(store: _store);
        //delete the luggage from the store after claiming it
        _transformerAsync.InitializeUnwrapFromAttributeParams(false);
        _contents = DataGenerator.CreateString(6000);
    }

    [Fact]
    public async Task When_a_message_unwraps_a_large_payload()
    {
        //arrange
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(_contents);
        await writer.FlushAsync();
        stream.Position = 0;
        
        var id = await _store.StoreAsync(stream);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody("Claim Check {id}"));
        message.Header.Bag[ClaimCheckTransformerAsync.CLAIM_CHECK] = id;
        
        //act
        var unwrappedMessage = await _transformerAsync.UnwrapAsync(message);
        
        //assert
        unwrappedMessage.Body.Value.Should().Be(_contents);
        //clean up
        message.Header.Bag.TryGetValue(ClaimCheckTransformerAsync.CLAIM_CHECK, out object _).Should().BeFalse();
        (await _store.HasClaimAsync(id)).Should().BeFalse();
    }
}
