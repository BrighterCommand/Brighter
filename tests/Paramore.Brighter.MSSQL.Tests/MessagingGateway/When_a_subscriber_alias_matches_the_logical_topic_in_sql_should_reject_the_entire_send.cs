#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

[Trait("Category", "MSSQL")]
[Collection("MSSQLMessagingGateway")]
public class MsSqlSubscriberReadinessTests : IDisposable
{
    private readonly RelationalDatabaseConfiguration _configuration = new MsSqlTestHelper().QueueConfiguration;
    private readonly RoutingKey _topic = new("logical.event");
    private readonly MsSqlMessageProducer _producer;
    private string? _purgeGateTable;

    public MsSqlSubscriberReadinessTests()
    {
        _producer = (MsSqlMessageProducer)new MsSqlMessageProducerFactory(_configuration,
            [new MsSqlPublication { Topic = _topic, SubscriberChannels = [new("first"), new("second")] }])
            .Create().Single().Value;
    }

    [Theory]
    [InlineData(false, "Latin1_General_CI_AI", "CAFE")]
    [InlineData(true, "Latin1_General_CI_AI", "CAFE")]
    [InlineData(false, "Latin1_General_CI_AI", "café ")]
    [InlineData(true, "Latin1_General_CI_AI", "café ")]
    [InlineData(false, "Latin1_General_100_BIN2", "café ")]
    [InlineData(true, "Latin1_General_100_BIN2", "café ")]
    public async Task When_a_subscriber_alias_matches_the_logical_topic_in_sql_should_reject_the_entire_send(
        bool async, string collation, string alias)
    {
        //Arrange
        SetCollation(collation);
        using var producer = new MsSqlMessageProducer(_configuration,
            new MsSqlPublication { Topic = new("café"), SubscriberChannels = [new("other"), new(alias)] });
        var message = Message(new RoutingKey("café"));

        //Act
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => Send(producer, message, async));

        //Assert
        Assert.Contains("logical topic", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Scalar($"SELECT COUNT(*) FROM [{_configuration.QueueStoreTable}]"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_sql_distinguishes_the_topic_from_a_case_variant_should_allow_the_subscriber(bool async)
    {
        //Arrange
        SetCollation("Latin1_General_100_BIN2");
        using var producer = new MsSqlMessageProducer(_configuration,
            new MsSqlPublication { Topic = new("topic"), SubscriberChannels = [new("TOPIC")] });
        using var consumer = Consumer("TOPIC");
        var message = Message(new RoutingKey("topic"));

        //Act
        await Send(producer, message, async);

        //Assert
        Assert.Equal(message.Id, (await Receive(consumer)).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_plain_queue_names_differ_only_in_spelling_should_keep_topic_scheduling(bool async)
    {
        //Arrange
        SetCollation("Latin1_General_CI_AI");
        var delivered = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scheduler = new ConformanceHarnessMessageScheduler(message =>
        {
            delivered.TrySetResult(message);
            return null;
        });
        var factory = new MsSqlMessageConsumerFactory(_configuration, scheduler);
        var subscription = new MsSqlSubscription(new("plain"), new("CAFE "), new("café"),
            requestType: typeof(Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand), requeueDelay: TimeSpan.FromMilliseconds(10));
        var message = Message(new RoutingKey("café"));

        //Act
        using var consumer = async ? (MsSqlMessageConsumer)factory.CreateAsync(subscription) : (MsSqlMessageConsumer)factory.Create(subscription);
        var accepted = async
            ? await consumer.RequeueAsync(message, subscription.RequeueDelay)
            : consumer.Requeue(message, subscription.RequeueDelay);

        //Assert
        Assert.True(accepted);
        Assert.Same(message, await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("café", message.Header.Topic.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_the_callers_publication_is_mutated_should_keep_the_producers_routing_snapshot(bool async)
    {
        //Arrange
        var channels = new List<ChannelName> { new("first"), new("second") };
        var publication = new MsSqlPublication { Topic = _topic, SubscriberChannels = channels };
        using var producer = new MsSqlMessageProducer(_configuration, publication);
        using var first = Consumer("first");
        using var second = Consumer("second");
        using var unexpected = Consumer("unexpected");
        var message = Message(_topic);
        channels.Clear();
        channels.Add(new("unexpected"));
        publication.Topic = new("changed.topic");

        //Act
        await Send(producer, message, async);

        //Assert
        Assert.Equal(message.Id, (await Receive(first)).Id);
        Assert.Equal(message.Id, (await Receive(second)).Id);
        Assert.True((await Receive(unexpected)).IsEmpty);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_a_fan_out_transaction_is_open_should_skip_its_copies_and_read_committed_queues(
        bool async, bool commit)
    {
        //Arrange
        var committed = Message(_topic);
        var pending = Message(_topic);
        await Send(_producer, committed, async);
        using var otherProducer = new MsSqlMessageProducer(_configuration);
        var unrelated = Message(new RoutingKey("unrelated"));
        await Send(otherProducer, unrelated, async);
        using var first = Consumer("first");
        using var second = Consumer("second");
        using var other = Consumer("unrelated");

        //Act
        using (var transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                   new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                   TransactionScopeAsyncFlowOption.Enabled))
        {
            await Send(_producer, pending, async);
            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                //Assert
                Assert.Equal(committed.Id, (await Receive(first)).Id);
                Assert.Equal(committed.Id, (await Receive(second)).Id);
                Assert.Equal(unrelated.Id, (await Receive(other)).Id);
                Assert.True((await Receive(first)).IsEmpty);
                Assert.True((await Receive(second)).IsEmpty);
            }
            if (commit)
                transaction.Complete();
        }

        //Assert
        var firstCopy = await Receive(first);
        var secondCopy = await Receive(second);
        if (commit)
        {
            Assert.Equal(pending.Id, firstCopy.Id);
            Assert.Equal(pending.Id, secondCopy.Id);
        }
        else
        {
            Assert.True(firstCopy.IsEmpty);
            Assert.True(secondCopy.IsEmpty);
        }
        Assert.True((await Receive(first)).IsEmpty);
        Assert.True((await Receive(second)).IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_fan_out_send_is_blocked_should_honor_the_publication_timeout(bool async)
    {
        //Arrange
        using var producer = new MsSqlMessageProducer(_configuration,
            new MsSqlPublication { Topic = _topic, SubscriberChannels = [new("first"), new("second")], SendTimeout = TimeSpan.FromSeconds(1) });
        using var blocker = new SqlConnection(_configuration.ConnectionString);
        await blocker.OpenAsync();
        using var transaction = blocker.BeginTransaction();
        using var command = blocker.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT(*) FROM [{_configuration.QueueStoreTable}] WITH (TABLOCKX, HOLDLOCK)";
        await command.ExecuteScalarAsync();
        Task? sending = null;

        try
        {
            //Act
            sending = Task.Run(() => Send(producer, Message(_topic), async));
            var error = await Assert.ThrowsAsync<SqlException>(() => sending.WaitAsync(TimeSpan.FromSeconds(5)));

            //Assert
            Assert.Equal(-2, error.Number);
        }
        finally
        {
            transaction.Rollback();
            if (sending != null)
                await Record.ExceptionAsync(() => sending);
        }

        //Assert
        Assert.Equal(0, await Scalar($"SELECT COUNT(*) FROM [{_configuration.QueueStoreTable}]"));
        await Send(producer, Message(_topic), async);
        Assert.Equal(2, await Scalar($"SELECT COUNT(*) FROM [{_configuration.QueueStoreTable}]"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_large_purge_is_in_progress_should_allow_other_queues_and_preserve_later_sends(bool async)
    {
        //Arrange
        var seed = Message(_topic);
        await _producer.SendAsync(seed);
        await Scalar($"""
            INSERT INTO [{_configuration.QueueStoreTable}] (Topic, MessageType, Payload)
            SELECT TOP (12000) N'first', queued.MessageType, queued.Payload
            FROM [{_configuration.QueueStoreTable}] AS queued
            CROSS JOIN sys.all_objects AS a CROSS JOIN sys.all_objects AS b
            WHERE queued.Topic = N'first';
            """);
        var gateTable = "purge_gate_" + Guid.NewGuid().ToString("N");
        await Scalar($"CREATE TABLE [{gateTable}] (QueueId bigint NOT NULL PRIMARY KEY REFERENCES [{_configuration.QueueStoreTable}](Id) ON DELETE CASCADE)");
        _purgeGateTable = gateTable;
        await Scalar($"INSERT INTO [{gateTable}] (QueueId) SELECT Id FROM [{_configuration.QueueStoreTable}] WHERE Topic=N'first'");
        var barrier = "purge4665_" + Guid.NewGuid().ToString("N");
        using var blocker = new SqlConnection(_configuration.ConnectionString);
        await blocker.OpenAsync();
        using var hold = blocker.CreateCommand();
        hold.CommandText = $"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource=N'{barrier}', @LockMode='Exclusive', @LockOwner='Session'; SELECT @r;";
        Assert.True(Convert.ToInt32(await hold.ExecuteScalarAsync()) >= 0);
        await Scalar($"""
            CREATE TRIGGER [purge_{Guid.NewGuid():N}] ON [{gateTable}] AFTER DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF NOT EXISTS (SELECT 1 FROM deleted) RETURN;
                DECLARE @r int;
                EXEC @r = sys.sp_getapplock @Resource=N'{barrier}', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
                IF @r < 0 THROW 51002, 'Purge test barrier timed out', 1;
            END;
            """);
        using var first = Consumer("first");
        using var second = Consumer("second");
        using var producer = new MsSqlMessageProducer(_configuration);
        var later = Message(new RoutingKey("first"));
        var purging = Task.Run(async () =>
        {
            if (async)
                await first.PurgeAsync();
            else
                first.Purge();
        });

        try
        {
            //Act
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (await Scalar($"SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID() AND resource_type='APPLICATION' AND request_status='WAIT' AND resource_description LIKE '%{barrier[..32]}%'") == 0)
                await Task.Delay(20, cancellation.Token);

            //Assert
            Assert.False(purging.IsCompleted);
            Assert.Equal(seed.Id, (await Receive(second)).Id);
            await producer.SendAsync(later, cancellation.Token);
        }
        finally
        {
            using var release = blocker.CreateCommand();
            release.CommandText = $"EXEC sys.sp_releaseapplock @Resource=N'{barrier}', @LockOwner='Session';";
            await release.ExecuteNonQueryAsync();
            await purging.WaitAsync(TimeSpan.FromSeconds(15));
        }

        //Assert
        Assert.Equal(later.Id, (await Receive(first)).Id);
        Assert.True((await Receive(first)).IsEmpty);
        Assert.True((await Receive(second)).IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_independent_subscriber_mode_is_explicit_should_use_targeted_scheduling(bool async)
    {
        //Arrange
        var scheduler = new TestDoubles.InMemorySubscriberRetryScheduler();
        var subscription = new MsSqlSubscription(new("subscriber"), new("same"), new("same"),
            requestType: typeof(Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand)) { IsIndependentSubscriber = true };
        var factory = new MsSqlMessageConsumerFactory(_configuration, scheduler);
        using var consumer = async ? (MsSqlMessageConsumer)factory.CreateAsync(subscription) : (MsSqlMessageConsumer)factory.Create(subscription);
        var message = Message(new RoutingKey("same"));
        var delay = TimeSpan.FromSeconds(1);

        //Act
        var accepted = async ? await consumer.RequeueAsync(message, delay) : consumer.Requeue(message, delay);

        //Assert
        Assert.True(accepted);
        Assert.Same(message, scheduler.Message);
        Assert.Equal("same", scheduler.Destination?.Value);
        Assert.Equal(delay, scheduler.Delay);
    }

    private void SetCollation(string collation)
    {
        using var connection = new SqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            DROP INDEX [IX_{_configuration.QueueStoreTable}_Topic] ON [{_configuration.QueueStoreTable}];
            ALTER TABLE [{_configuration.QueueStoreTable}] ALTER COLUMN Topic nvarchar(255) COLLATE {collation} NOT NULL;
            CREATE INDEX [IX_{_configuration.QueueStoreTable}_Topic] ON [{_configuration.QueueStoreTable}](Topic);
            """;
        command.ExecuteNonQuery();
    }

    private async Task<int> Scalar(string sql)
    {
        using var connection = new SqlConnection(_configuration.ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandTimeout = 5;
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private MsSqlMessageConsumer Consumer(string channel) =>
        (MsSqlMessageConsumer)new MsSqlMessageConsumerFactory(_configuration).Create(
            new MsSqlSubscription(new(Guid.NewGuid().ToString()), new(channel), _topic,
                requestType: typeof(Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand)) { IsIndependentSubscriber = true });

    private static Message Message(RoutingKey topic) =>
        new(new MessageHeader(Id.Random(), topic, MessageType.MT_EVENT), new MessageBody("payload"));

    private static Task Send(MsSqlMessageProducer producer, Message message, bool async)
    {
        if (async)
            return producer.SendAsync(message);
        producer.Send(message);
        return Task.CompletedTask;
    }

    private static async Task<Message> Receive(MsSqlMessageConsumer consumer)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return (await consumer.ReceiveAsync(TimeSpan.FromSeconds(5), cancellation.Token)).Single();
    }

    public void Dispose()
    {
        _producer.Dispose();
        if (_purgeGateTable != null)
            Configuration.DeleteTable(_configuration.ConnectionString, $"[{_purgeGateTable}]");
        Configuration.DeleteTable(_configuration.ConnectionString, $"[{_configuration.QueueStoreTable}]");
    }
}
