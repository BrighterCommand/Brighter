# Brighter MSSQL outbox

## Setup

To setup Brighter with a SQL Server or SQL CE outbox, some steps are required:

#### Create a table with the schema in the example

You can use the following example as a reference for SQL Server:

```sql
        CREATE TABLE MyOutbox (
            Id uniqueidentifier CONSTRAINT PK_MessageId PRIMARY KEY,
            Topic nvarchar(255),
            MessageType nvarchar(32),
            Body nvarchar(max)
        )
```
If you're using SQL CE you have to replace `nvarchar(max)` with a supported type, for example `ntext`.

#### Configure the command processor

The following is an example of how to configure a command processor with a SQL Server outbox.

```csharp
var msSqlOutbox = new MsSqlOutbox(new MsSqlOutboxConfiguration(
        "myconnectionstring", 
        "MyOutboxTable", 
        MsSqlOutboxConfiguration.DatabaseType.MsSqlServer
    ), myLogger),

var commandProcessor = CommandProcessorBuilder.With()
    .Handlers(new HandlerConfiguration(mySubscriberRegistry, myHandlerFactory))
    .Policies(myPolicyRegistry)
    .Logger(myLogger)
    .TaskQueues(new MessagingConfiguration(
        outbox: msSqlOutbox,
        messagingGateway: myGateway,
        messageMapperRegistry: myMessageMapperRegistry
        ))
    .RequestContextFactory(new InMemoryRequestContextFactory())
    .Build();
```

> The values for the `MsSqlOutboxConfiguration.DatabaseType` enum are the following:  
> `MsSqlServer`  
> `SqlCe`
