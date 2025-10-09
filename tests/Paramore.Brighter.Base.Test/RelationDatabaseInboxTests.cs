using System;

namespace Paramore.Brighter.Base.Test;

public abstract class RelationalDatabaseInboxTests : InboxTests
{
    private RelationalDatabaseInbox? _inbox;
    protected override IAmAnInboxSync Inbox => _inbox ?? throw new InvalidOperationException();
    
    protected RelationalDatabaseConfiguration? Configuration { get; private set; } 
    
    protected abstract string DefaultConnectingString { get; }
    protected abstract string TableNamePrefix { get; }
    protected abstract bool BinaryMessagePayload { get; }
    protected virtual string? SchemaName { get; } = null;
    protected abstract RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration);
    protected abstract void CreateInboxTable(RelationalDatabaseConfiguration configuration);
    protected abstract void DeleteInboxTable(RelationalDatabaseConfiguration configuration);

    protected override void BeforeEachTest()
    { 
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectingString;
        }
        
        Configuration = new RelationalDatabaseConfiguration(connectionString, 
            inboxTableName: $"{TableNamePrefix}{Uuid.New():N}",
            schemaName: SchemaName,
            binaryMessagePayload: BinaryMessagePayload);
        
        _inbox = CreateInbox(Configuration);
        base.BeforeEachTest();
    }

    protected override void CreateStore()
    {
        CreateInboxTable(Configuration!);
    }

    protected override void DeleteStore()
    {
        DeleteInboxTable(Configuration!);
        base.DeleteStore();
    }
}
