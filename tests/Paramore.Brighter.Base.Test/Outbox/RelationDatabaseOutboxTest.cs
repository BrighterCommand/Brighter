using System;
using System.Collections.Generic;
using System.Data.Common;

namespace Paramore.Brighter.Base.Test.Outbox;

public abstract class RelationDatabaseOutboxTest : OutboxTest<DbTransaction>
{
    private RelationDatabaseOutbox? _outbox;

    protected override IAmAnOutboxSync<Message, DbTransaction> Outbox => _outbox ?? throw new InvalidOperationException();

    protected RelationalDatabaseConfiguration? Configuration { get; private set; } 
    protected abstract string DefaultConnectingString { get; }
    protected abstract string TableNamePrefix { get; }
    protected abstract bool BinaryMessagePayload { get; }
    protected virtual string? SchemaName { get; } = null;
    
    protected abstract RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration);
    
    protected abstract void CreateOutboxTable(RelationalDatabaseConfiguration configuration);
    
    protected abstract void DeleteOutboxTable(RelationalDatabaseConfiguration configuration);
    
    protected override void BeforeEachTest()
    { 
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectingString;
        }
        
        Configuration = new RelationalDatabaseConfiguration(connectionString, 
            outBoxTableName: $"{TableNamePrefix}{Uuid.New():N}",
            schemaName: SchemaName,
            binaryMessagePayload: BinaryMessagePayload);
        
        _outbox = CreateOutbox(Configuration);
        base.BeforeEachTest();
    }

    protected override void CreateStore()
    {
        CreateOutboxTable(Configuration!);
    }

    protected override void DeleteStore()
    {
        DeleteOutboxTable(Configuration!);
        base.DeleteStore();
    }


    protected override IEnumerable<Message> GetAllMessages()
    {
        return _outbox!.Get(null);
    }
}
