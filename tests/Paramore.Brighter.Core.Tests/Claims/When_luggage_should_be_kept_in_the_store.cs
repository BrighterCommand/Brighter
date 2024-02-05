using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class RetrieveClaimLeaveLuggage
{
    private readonly InMemoryStorageProvider _store;
    private readonly ClaimCheckTransformer _transformer;
    private readonly string _contents;

    public RetrieveClaimLeaveLuggage()
    {
        _store = new InMemoryStorageProvider();
        _transformer = new ClaimCheckTransformer(store: _store);
        _transformer.InitializeUnwrapFromAttributeParams(true);
        
        _contents = DataGenerator.CreateString(6000);
    }

    [Fact]
    public void When_luggage_should_be_kept_in_the_store()
    {
        //arrange
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.WriteAsync(_contents);
        writer.FlushAsync();
        stream.Position = 0;

        var id = _store.Store(stream);

        var message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),
            new MessageBody("Claim Check {id}"));
        message.Header.Bag[ClaimCheckTransformerAsync.CLAIM_CHECK] = id;

        //act
        var unwrappedMessage = _transformer.Unwrap(message);
        
        //assert
        message.Header.Bag.TryGetValue(ClaimCheckTransformerAsync.CLAIM_CHECK, out object _).Should().BeTrue();
        _store.HasClaim(id).Should().BeTrue();
        
    }
}
