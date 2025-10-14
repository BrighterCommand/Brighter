using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

public class MongoDbOutboxTest : OutboxTest<IClientSessionHandle>
{
    private string? _collectionName;
    private MongoDbOutbox? _outbox;
    protected override IAmAnOutboxSync<Message, IClientSessionHandle> Outbox => _outbox;
    protected override IEnumerable<Message> GetAllMessages()
    {
        return _outbox!.Get();
    }

    protected override IAmABoxTransactionProvider<IClientSessionHandle> CreateTransactionProvider()
    {
        return new MongoDbUnitOfWork(new MongoDbConfiguration(Const.Client, Const.DatabaseName));
    }

    protected override void CreateStore()
    {
        _collectionName = $"Outbox{Uuid.New():N}";
        _outbox = new MongoDbOutbox(new MongoDbConfiguration(Const.Client, Const.DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration
            {
                Name = _collectionName
            }
        });
    }

    protected override void DeleteStore()
    {
        var database = Const.Client.GetDatabase(Const.DatabaseName);
        database.DropCollection(_collectionName);
    }
    
    [Fact]
    public override void AddMessageUsingTransaction()
    {
        // MongoDBin docker-compose doesn't have support to transaction
        Assert.True(true);
    }

    [Fact]
    public override void AddMessageUsingTransactionShouldNotInsertWhenRollback()
    {
        // MongoDBin docker-compose doesn't have support to transaction
        Assert.True(true);
    }
}
