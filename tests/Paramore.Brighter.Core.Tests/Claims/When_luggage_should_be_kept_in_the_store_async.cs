using System;
using System.IO;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Claims;

public class AsyncRetrieveClaimLeaveLuggage
{
    private readonly InMemoryStorageProviderAsync _store;
    private readonly ClaimCheckTransformerAsync _transformerAsync;
    private readonly string _contents;

    public AsyncRetrieveClaimLeaveLuggage()
    {
        _store = new InMemoryStorageProviderAsync();
        _transformerAsync = new ClaimCheckTransformerAsync(store: _store);
        _transformerAsync.InitializeUnwrapFromAttributeParams(true);

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
            new MessageHeader(Guid.NewGuid().ToString(), new("test_topic"), MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody($"Claim Check {id}"));
        //act
        var unwrappedMessage = await _transformerAsync.UnwrapAsync(message);

        //assert
        bool hasLuggage = message.Header.Bag.TryGetValue(ClaimCheckTransformerAsync.CLAIM_CHECK, out object _);
        Assert.True(hasLuggage);
        Assert.True(await _store.HasClaimAsync(id));
    }
}
