#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway.Provisioning;

/// <summary>
/// OnMissingChannel.Create is the default on MsSqlSubscription and on Publication, and until now it
/// was accepted and never acted on by this gateway — the PostgreSQL gateway creates its queue table,
/// this one did not. These tests are the specification for that behaviour arriving.
/// </summary>
[Collection("MsSqlQueueProvisioning")]
public class MsSqlQueueProvisioningCreateTests : IDisposable
{
    private readonly string _queueTable = UniqueQueueTableName();
    private readonly RelationalDatabaseConfiguration _configuration;

    public MsSqlQueueProvisioningCreateTests()
    {
        Configuration.EnsureDatabaseExists(Configuration.DefaultConnectingString);
        _configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString, queueStoreTable: _queueTable);
    }

    [Fact]
    public void When_the_subscription_creates_a_missing_queue_should_create_the_table_and_index()
    {
        //Arrange
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));
        var subscription = new MsSqlSubscription<MyCommand>(
            new SubscriptionName("create.subscription"),
            new ChannelName("create.channel"),
            new RoutingKey("create.topic"),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        //Act
        using var channel = channelFactory.CreateSyncChannel(subscription);

        //Assert
        Assert.True(QueueTableExists(_queueTable));
        Assert.True(TopicIndexExists(_queueTable));
    }

    [Fact]
    public void When_the_queue_is_created_twice_should_not_throw()
    {
        //Arrange -- idempotence is what makes this safe to run on every start, which is the whole
        //premise of doing it at channel-open time rather than in a migration. Two factories rather
        //than one call twice, because a gateway instance now remembers that it has provisioned:
        //against a single factory this would prove nothing but that the second call returned early,
        //and the claim under test is about a second *start*.
        var subscription = new MsSqlSubscription<MyCommand>(
            new SubscriptionName("create.subscription"),
            new ChannelName("create.channel"),
            new RoutingKey("create.topic"),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);
        using (var first = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration))
                   .CreateSyncChannel(subscription)) { }

        //Act
        var exception = Record.Exception(() =>
        {
            using var second = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration))
                .CreateSyncChannel(subscription);
        });

        //Assert
        Assert.Null(exception);
        Assert.True(QueueTableExists(_queueTable));
    }

    [Fact]
    public void When_the_publication_creates_a_missing_queue_should_create_the_table_and_index()
    {
        //Arrange -- the producer side provisions too, because a sender may start before any
        //consumer has ever run.
        var publication = new Publication
        {
            Topic = new RoutingKey("create.topic"), MakeChannels = OnMissingChannel.Create
        };
        var producerFactory = new MsSqlMessageProducerFactory(
            _configuration, new List<Publication> { publication });

        //Act
        producerFactory.Create();

        //Assert
        Assert.True(QueueTableExists(_queueTable));
        Assert.True(TopicIndexExists(_queueTable));
    }

    [Fact]
    public void When_the_queue_table_name_would_close_the_ddl_bracket_should_throw_before_touching_the_database()
    {
        //Arrange -- the DDL formats the name into CREATE TABLE [{0}] and escapes nothing, so a ']'
        //closes the bracket and everything after it is free SQL. The guard belongs here, beside
        //the string.Format, not in a caller.
        //
        //Pointed at a server that cannot be reached, so that "before touching the database" is what
        //the test measures rather than something its name merely asserts: against a live connection
        //string this would pass equally if the guard ran after the connect.
        var configuration = new RelationalDatabaseConfiguration(
            MsSqlQueueProvisioningAssumeTests.UnreachableConnectionString,
            queueStoreTable: "Queue]; DROP TABLE Users--");
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));
        var subscription = new MsSqlSubscription<MyCommand>(
            new SubscriptionName("create.subscription"),
            new ChannelName("create.channel"),
            new RoutingKey("create.topic"),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        //Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(subscription));

        //Assert
        Assert.IsType<ConfigurationException>(exception);
    }

    [Fact]
    public void When_the_queue_table_name_is_a_guid_with_hyphens_should_provision_it()
    {
        //Arrange -- a bracketed identifier legally holds hyphens, and the gateway's own test suite
        //names queue tables this way. A guard that demanded a plain unquoted identifier would
        //reject names this gateway has always accepted, which is a breaking change wearing the
        //costume of a security fix.
        var hyphenated = "queue_test_" + Guid.NewGuid();
        var configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString, queueStoreTable: hyphenated);
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        try
        {
            //Act
            using var channel = channelFactory.CreateSyncChannel(new MsSqlSubscription<MyCommand>(
                new SubscriptionName("create.subscription"),
                new ChannelName("create.channel"),
                new RoutingKey("create.topic"),
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create));

            //Assert
            Assert.True(QueueTableExists(hyphenated));
        }
        finally
        {
            DropQueueTable(hyphenated);
        }
    }

    [Fact]
    public void When_the_queue_table_name_is_longer_than_sql_servers_limit_should_throw()
    {
        //Arrange -- 129 characters; 128 is SQL Server's own identifier limit. Without a bound the
        //name reaches the CREATE and fails there with a message that never mentions configuration.
        //Unreachable server for the same reason as the bracket test above.
        var configuration = new RelationalDatabaseConfiguration(
            MsSqlQueueProvisioningAssumeTests.UnreachableConnectionString,
            queueStoreTable: new string('Q', 129));
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));
        var subscription = new MsSqlSubscription<MyCommand>(
            new SubscriptionName("create.subscription"),
            new ChannelName("create.channel"),
            new RoutingKey("create.topic"),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        //Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(subscription));

        //Assert
        Assert.IsType<ConfigurationException>(exception);
    }

    [Fact]
    public void When_the_queue_table_name_leaves_no_room_for_the_index_name_should_throw()
    {
        //Arrange -- 120 characters, which SQL Server accepts as a table name and rejects as the
        //index name derived from it: CREATE NONCLUSTERED INDEX [IX_{table}_Topic] is nine
        //characters longer, so 129 and error 103, "The identifier that starts with ... is too
        //long". Measured against SQL Server, where the CREATE TABLE succeeds first and the index
        //then fails, leaving a table that can never gain its index.
        var configuration = new RelationalDatabaseConfiguration(
            MsSqlQueueProvisioningAssumeTests.UnreachableConnectionString,
            queueStoreTable: new string('Q', 120));
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        //Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(
            new MsSqlSubscription<MyCommand>(
                new SubscriptionName("create.subscription"),
                new ChannelName("create.channel"),
                new RoutingKey("create.topic"),
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create)));

        //Assert
        var configurationException = Assert.IsType<ConfigurationException>(exception);
        Assert.Contains("119", configurationException.Message);
    }

    [Fact]
    public void When_the_queue_table_name_is_as_long_as_the_index_name_allows_should_provision_it()
    {
        //Arrange -- the control for the fact above, and the one that stops the bound from being
        //tightened arbitrarily: 119 characters yields an index name of exactly 128, which SQL
        //Server accepts. A guard that rejected this would be refusing a name the database takes.
        var longest = "Q" + new string('q', 118);
        var configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString, queueStoreTable: longest);
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        try
        {
            //Act
            using var channel = channelFactory.CreateSyncChannel(new MsSqlSubscription<MyCommand>(
                new SubscriptionName("create.subscription"),
                new ChannelName("create.channel"),
                new RoutingKey("create.topic"),
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create));

            //Assert
            Assert.True(QueueTableExists(longest));
            Assert.True(TopicIndexExists(longest));
        }
        finally
        {
            DropQueueTable(longest);
        }
    }

    [Fact]
    public void When_the_index_naming_convention_is_read_from_the_builder_should_still_be_the_one_the_guard_assumes()
    {
        //Arrange -- the guard above hard-codes "IX_" and "_Topic" because MsSqlQueueBuilder owns
        //that convention and exposes only the finished DDL. If the convention ever moves, the bound
        //silently stops matching the identifier it is bounding; this is what fails instead.
        const string table = "SomeQueueTable";

        //Act
        var ddl = MsSqlQueueBuilder.GetIndexDDL(table);

        //Assert
        Assert.Contains($"[IX_{table}_Topic]", ddl);
    }

    internal static string UniqueQueueTableName() => "Queue_" + Guid.NewGuid().ToString("N");

    internal static bool QueueTableExists(string queueTable)
    {
        using var connection = new SqlConnection(Configuration.DefaultConnectingString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT COUNT(1) FROM sys.tables t
                              INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                              WHERE t.name = @queueTable AND s.name = SCHEMA_NAME()
                              """;
        command.Parameters.AddWithValue("@queueTable", queueTable);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    internal static bool TopicIndexExists(string queueTable)
    {
        //Guarded on what the index IS -- a non-primary-key index leading on Topic -- rather than on
        //what it is called, so a rename does not read as a missing index.
        using var connection = new SqlConnection(Configuration.DefaultConnectingString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT COUNT(1) FROM sys.indexes i
                              INNER JOIN sys.index_columns ic
                                  ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                              INNER JOIN sys.columns c
                                  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                              WHERE i.object_id = OBJECT_ID(QUOTENAME(SCHEMA_NAME()) + '.' + QUOTENAME(@queueTable))
                                AND i.is_primary_key = 0
                                AND ic.key_ordinal = 1
                                AND c.name = 'Topic'
                              """;
        command.Parameters.AddWithValue("@queueTable", queueTable);
        return Convert.ToInt32(command.ExecuteScalar()) >= 1;
    }

    internal static void DropQueueTable(string queueTable)
    {
        using var connection = new SqlConnection(Configuration.DefaultConnectingString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS [{queueTable}]";
        command.ExecuteNonQuery();
    }

    public void Dispose() => DropQueueTable(_queueTable);

    private class MyCommand : Command
    {
        public MyCommand() : base(Guid.NewGuid()) { }
    }
}
