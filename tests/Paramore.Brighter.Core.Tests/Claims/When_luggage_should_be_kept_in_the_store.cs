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
    private readonly InMemoryStorageProviderAsync _store;
    private readonly ClaimCheckTransformer _transformer;
    private readonly string _contents;

    public RetrieveClaimLeaveLuggage()
    {
        _store = new InMemoryStorageProviderAsync();
        _transformer = new ClaimCheckTransformer(store: _store);
        _transformer.InitializeUnwrapFromAttributeParams(true);

        _contents = DataGenerator.CreateString(6000);
    }

    [Fact]
    public async Task When_luggage_should_be_kept_in_the_store()
    {
        //arrange
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(_contents);
        await writer.FlushAsync();
        stream.Position = 0;

        var id = await _store.StoreAsync(stream);

        var message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),
            new MessageBody("Claim Check {id}"));
        message.Header.Bag[ClaimCheckTransformer.CLAIM_CHECK] = id;

        //act
        var unwrappedMessage = await _transformer.UnwrapAsync(message);

        //assert
        message.Header.Bag.TryGetValue(ClaimCheckTransformer.CLAIM_CHECK, out object _).Should().BeTrue();
        (await _store.HasClaimAsync(id)).Should().BeTrue();
    }
}
