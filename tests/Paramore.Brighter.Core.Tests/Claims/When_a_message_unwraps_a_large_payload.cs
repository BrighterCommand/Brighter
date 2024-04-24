using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class RetrieveClaimLargePayloadTests
{
    private readonly InMemoryStorageProvider _store;
    private readonly ClaimCheckTransformer _transformerAsync;
    private readonly string _contents;

    public RetrieveClaimLargePayloadTests()
    {
        _store = new InMemoryStorageProvider();
        _transformerAsync = new ClaimCheckTransformer(store: _store);
        //delete the luggage from the store after claiming it
        _transformerAsync.InitializeUnwrapFromAttributeParams(false);
        _contents = DataGenerator.CreateString(6000);
    }

    [Fact]
    public void When_a_message_unwraps_a_large_payload()
    {
        //arrange
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(_contents);
        writer.Flush();
        stream.Position = 0;
        
        var id = _store.Store(stream);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody("Claim Check {id}"));
        message.Header.Bag[ClaimCheckTransformerAsync.CLAIM_CHECK] = id;
        
        //act
        var unwrappedMessage = _transformerAsync.Unwrap(message);
        
        //assert
        unwrappedMessage.Body.Value.Should().Be(_contents);
        //clean up
        message.Header.Bag.TryGetValue(ClaimCheckTransformerAsync.CLAIM_CHECK, out object _).Should().BeFalse();
        _store.HasClaim(id).Should().BeFalse();
    }
}
