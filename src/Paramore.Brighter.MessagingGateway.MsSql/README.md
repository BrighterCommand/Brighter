# Brighter MSSQL messaging gateway

## Don't

Queues in databases are a bad idea for many reasons. See for instance: http://mikehadlow.blogspot.nl/2012/04/database-as-queue-anti-pattern.html.

So don't do it.

Really.

I'm serious.

But...

You may encounter a situation where you 
- have no influence on your customers infrastructure
- need to decouple long running processing from an interactive process
- need this decoupling be durable and persist across re-boots
- need a single (physical) queue where multiple producers and multiple consumers can share a topic
- **have a LOW volume of messages**

Then your solution may be the use of this implementation of a Brighter Messaging Gateway using SQL Server.

## Setup

You need SQL Server 2005 or newer (see below as to why).

To setup Brighter with a SQL Server based messaging gateway, some steps are required:

#### Create a table with the schema as shown by the QueueStore.sql example

You can use the following example as a reference for SQL Server:

```sql
        PRINT 'Creating Queue table'
        CREATE TABLE [dbo].[QueueData](
            [Id] [bigint] IDENTITY(1,1) NOT NULL,
            [Topic] [nvarchar](255) NOT NULL,
            [MessageType] [nvarchar](1024) NOT NULL,
            [Payload] [nvarchar](max) NOT NULL,
        CONSTRAINT [PK_QueueData] PRIMARY KEY CLUSTERED 
        (
            [Id] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
        ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
        GO

        PRINT 'Creating an index on the Topic column of the Queue table...'
        CREATE NONCLUSTERED INDEX [IX_Topic] ON [dbo].[QueueData]
        (
            [Topic] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
        GO

        Print 'Done'
```

## Configuration

You specify the connection string to the database, and the name of the table that will hold the data.

#### Configure the command processor with a message producer

The following is an example of how to specify the configuration for the SQL Server messaging gateway to the command processor.

```csharp
        var messagingConfiguration = new MsSqlMessagingGatewayConfiguration(@"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;", "QueueData");
        var producer = new MsSqlMessageProducer(messagingConfiguration);

        var builder = CommandProcessorBuilder.With()
            .Handlers(new HandlerConfiguration())
            .DefaultPolicy()
            .TaskQueues(new MessagingConfiguration(messageStore, producer, messageMapperRegistry))
            .RequestContextFactory(new InMemoryRequestContextFactory());

        var commandProcessor = builder.Build();
```

#### Configure the dispatcher with a message consumer factory

The following is an example of how to specify the configuration for the SQL Server messaging gateway to the message dispatcher.

```csharp
        ...

        //create the gateway
        var messagingConfiguration =
            new MsSqlMessagingGatewayConfiguration(
                @"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;", "QueueData");
        var messageConsumerFactory = new MsSqlMessageConsumerFactory(messagingConfiguration);

        var dispatcher = DispatchBuilder.With()
            .CommandProcessor(CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(subscriberRegistry, handlerFactory))
                .Policies(policyRegistry)
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build())
            .MessageMappers(messageMapperRegistry)
            .DefaultChannelFactory(new MsSqlInputChannelFactory(messageConsumerFactory))
            .Connections(new Connection[]
            {
                new Connection<GreetingEvent>(
                    new ConnectionName("paramore.example.greeting"),
                    new ChannelName("greeting.event"),
                    new RoutingKey("greeting.event"),
                    timeoutInMilliseconds: 200)
            }).Build();

        dispatcher.Receive();

        ...
```

## Queuing details

#### FIFO

The SQL Server based messaging gateway implements a First In First Out queue.

Therefore, when using a single message consumer, messages are guaranteed to be retrieved in the same order as they were sent. The consumer may force
a message to be requeued by throwing the _DeferMessageAction_ exception. Of course such a message is by intention no longer in the correct order.

#### Competing consumers

When using the competing consumer pattern, multiple message consumers are available to handle messages of the same topic. This may lead to 
out of order handling of messages (caused by eg. thread availability, machine load, network load).

#### Guarantees

- when the commandprocessor Post or PostAsync completes without exceptions, your message is guaranteed to be on the queue.
- the message is guaranteed to be removed from the queue at the moment the dispatcher retrieves it (this is **before** your handler gets executed). 
Your registered handler is responsible for handling exceptional cases where you may need to requeue the message to a poison or dead letter queue.

#### Locking

Al sorts of concurrency and locking related problems are (hopefully) prevented by USING the **OUTPUT** Clause introduced first in SQL Server 
2005. See: http://rusanu.com/2010/03/26/using-tables-as-queues/. This also introduces a dependency on the version of SQL Server being 2005
or newer.

## Examples

See the samples\MsSqlMessagingGatewaySamples folders for examples on how to configure and use the SQL Server based messaging gateway.

#### Simple post and receive

- A console mode program to post a Greeting event (.NET Core)
- A console mode program to receive and process Greeting events (.NET Core)
- A Windows Service to receive and process Greeting events (.NET Framework)

#### Competing consumers

- A console mode program to receive and process Competing Consumer Commands (.NET Core), multiple instances may be started to process the available messages. 
The example handler will randomly throw an exception for 10% of the messages. This exception will be handled and cause the message to be reueued for later handling.
- A console mode program to send many Competing Consumer Commands (.NET Core) taking one argument: the number of messages to send, multiple instance may be started to send simultaneously.
This example will start a transactionscope, but never complete the transaction. This is done to illustrate the fact that a Post or PostAsync causes the message to 'escape' your domain
and can not be undone by rolling back the current transaction.
