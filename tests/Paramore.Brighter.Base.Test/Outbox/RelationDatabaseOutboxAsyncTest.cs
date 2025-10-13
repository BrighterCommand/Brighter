using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace Paramore.Brighter.Base.Test.Outbox;

public abstract class RelationDatabaseOutboxAsyncTest : OutboxAsyncTest<DbTransaction>
{
    private RelationDatabaseOutbox? _outbox;

    protected override IAmAnOutboxAsync<Message, DbTransaction> Outbox => _outbox ?? throw new InvalidOperationException();

    protected RelationalDatabaseConfiguration? Configuration { get; private set; } 
    protected abstract string DefaultConnectingString { get; }
    protected abstract string TableNamePrefix { get; }
    protected abstract bool BinaryMessagePayload { get; }
    protected virtual string? SchemaName { get; } = null;
    
    protected abstract RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration);
    
    protected abstract Task CreateOutboxTableAsync(RelationalDatabaseConfiguration configuration);
    
    protected abstract Task DeleteOutboxTableAsync(RelationalDatabaseConfiguration configuration);
    
    protected override async Task BeforeEachTestAsync()
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
        await base.BeforeEachTestAsync();
    }

    protected override async Task CreateStoreAsync()
    {
        await CreateOutboxTableAsync(Configuration!);
    }

    protected override async Task DeleteStoreAsync()
    {
        await DeleteOutboxTableAsync(Configuration!);
    }


    protected override async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        return await _outbox!.GetAsync(null);
    }
}
