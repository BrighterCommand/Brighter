# Brighter MSSQL message store

## Setup

To setup Brighter with a SQL Server or SQL CE message store, some steps are required:

#### Create a table with the schema in the example

You can use the following example as a reference for SQL Server:

```sql
        CREATE TABLE MyMessageStore (
            Id uniqueidentifier CONSTRAINT PK_MessageId PRIMARY KEY,
            Topic nvarchar(255),
            MessageType nvarchar(32),
            Body nvarchar(max)
        )
```
If you're using SQL CE you have to replace `nvarchar(max)` with a supported type, for example `ntext`.

#### Configure the command processor

The following is an example of how to configure a command processor with a SQL Server message store.

```csharp
var msSqlMessageStore = new MsSqlMessageStore(new MsSqlMessageStoreConfiguration(
        "myconnectionstring", 
        "MyMessageStoreTable", 
        MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer
    ), myLogger),

var commandProcessor = CommandProcessorBuilder.With()
    .Handlers(new HandlerConfiguration(mySubscriberRegistry, myHandlerFactory))
    .Policies(myPolicyRegistry)
    .Logger(myLogger)
    .TaskQueues(new MessagingConfiguration(
        messageStore: msSqlMessageStore,
        messagingGateway: myGateway,
        messageMapperRegistry: myMessageMapperRegistry
        ))
    .RequestContextFactory(new InMemoryRequestContextFactory())
    .Build();
```

> The values for the `MsSqlMessageStoreConfiguration.DatabaseType` enum are the following:  
> `MsSqlServer`  
> `SqlCe`
