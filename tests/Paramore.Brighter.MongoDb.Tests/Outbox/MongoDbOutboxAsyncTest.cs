using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

public class MongoDbOutboxAsyncTest : OutboxAsyncTest<IClientSessionHandle>
{
    private string? _collectionName;
    private MongoDbOutbox? _outbox;
    protected override IAmAnOutboxAsync<Message, IClientSessionHandle> Outbox => _outbox;
    protected override async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        return await _outbox!.GetAsync();
    }

    protected override IAmABoxTransactionProvider<IClientSessionHandle> CreateTransactionProvider()
    {
        return new MongoDbUnitOfWork(new MongoDbConfiguration(Const.Client, Const.DatabaseName));
    }

    protected override Task CreateStoreAsync()
    {
        _collectionName = $"Outbox{Uuid.New():N}";
        _outbox = new MongoDbOutbox(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });

        return base.CreateStoreAsync();
    }

    protected override async Task DeleteStoreAsync()
    {
        var database = Const.Client.GetDatabase(Const.DatabaseName);
        await database.DropCollectionAsync(_collectionName);
    }

    [Fact]
    public override Task When_Adding_A_Message_Within_Transaction_And_Rollback_It_Should_Not_Be_Stored()
    {
        // MongoDb On docker-compose doesn't support transaction
        Assert.True(true);
        return Task.CompletedTask;
    }
}
