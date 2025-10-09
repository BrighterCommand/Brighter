using System;
using System.Threading.Tasks;

namespace Paramore.Brighter.Base.Test;

public abstract class RelationalDatabaseInboxAsyncTests : InboxAsyncTest
{
    private RelationalDatabaseInbox? _inbox;
    protected override IAmAnInboxAsync Inbox => _inbox ?? throw new InvalidOperationException();
    protected RelationalDatabaseConfiguration? Configuration { get; private set; } 
    
    protected abstract string DefaultConnectingString { get; }
    protected abstract string TableNamePrefix { get; }
    protected abstract bool BinaryMessagePayload { get; }
    protected virtual string? SchemaName { get; } = null;
    protected abstract RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration);

    protected override async Task BeforeEachTestAsync()
    { 
        Configuration = new RelationalDatabaseConfiguration(DefaultConnectingString, 
            inboxTableName: $"{TableNamePrefix}{Uuid.New():N}",
            schemaName: SchemaName,
            binaryMessagePayload: BinaryMessagePayload);
        
        _inbox = CreateInbox(Configuration);
        await base.BeforeEachTestAsync();
    }
}
