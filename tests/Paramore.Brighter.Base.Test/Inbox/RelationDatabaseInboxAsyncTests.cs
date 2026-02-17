using System;
using System.Threading.Tasks;

namespace Paramore.Brighter.Base.Test.Inbox;

public abstract class RelationalDatabaseInboxAsyncTests : InboxAsyncTest
{
    private RelationalDatabaseInbox? _inbox;
    protected override IAmAnInboxAsync Inbox => _inbox ?? throw new InvalidOperationException();
    protected RelationalDatabaseConfiguration? Configuration { get; private set; } 
    
    protected abstract string DefaultConnectingString { get; }
    protected abstract string TableNamePrefix { get; }
    protected abstract bool BinaryMessagePayload { get; }
    protected abstract bool JsonMessagePayload { get; }
    protected virtual string? SchemaName { get; } = null;
    protected abstract RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration);
    protected abstract Task CreateInboxTableAsync(RelationalDatabaseConfiguration configuration);
    protected abstract Task DeleteInboxTableAsync(RelationalDatabaseConfiguration configuration);

    protected override async Task BeforeEachTestAsync()
    { 
       var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectingString;
        }
        
        Configuration = new RelationalDatabaseConfiguration(connectionString, 
            inboxTableName: $"{TableNamePrefix}{Uuid.New():N}",
            schemaName: SchemaName,
            binaryMessagePayload: BinaryMessagePayload,
            jsonMessagePayload: JsonMessagePayload);
        
        _inbox = CreateInbox(Configuration);
        await base.BeforeEachTestAsync();
    }

    protected override async Task CreateStoreAsync()
    {
        await CreateInboxTableAsync(Configuration!);
    }

    protected override async Task DeleteStoreAsync()
    {
        await DeleteInboxTableAsync(Configuration!);
    }
}
