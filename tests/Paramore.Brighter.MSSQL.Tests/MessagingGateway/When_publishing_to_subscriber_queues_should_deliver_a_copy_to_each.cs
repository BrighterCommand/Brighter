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
using System.Collections.Concurrent;
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
public class MsSqlFanOutTests : IDisposable
{
    private readonly RelationalDatabaseConfiguration _configuration = new MsSqlTestHelper().QueueConfiguration;
    private readonly RoutingKey _topic = new("logical.event");
    private readonly MsSqlPublication _publication;
    private readonly MsSqlMessageProducer _producer;
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    public MsSqlFanOutTests()
    {
        _publication = new MsSqlPublication
        {
            Topic = _topic,
            SubscriberChannels = [new ChannelName("first"), new ChannelName("second")]
        };
        _producer = (MsSqlMessageProducer)new MsSqlMessageProducerFactory(
            _configuration, [_publication]).Create().Single().Value;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_publishing_to_subscriber_queues_should_deliver_a_copy_to_each(bool async)
    {
        //Arrange
        var message = CreateMessage();
        message.Header.Bag["tenant"] = "customer";

        //Act
        await Send(message, async);
        using var first = Consumer("first");
        using var second = Consumer("second");
        var firstCopy = await Receive(first, async);
        var secondCopy = await Receive(second, async);

        //Assert
        AssertCopy(message, firstCopy);
        AssertCopy(message, secondCopy);
        Assert.Equal("customer", firstCopy.Header.Bag["tenant"].ToString());
        Assert.Equal("customer", secondCopy.Header.Bag["tenant"].ToString());
        Assert.True((await Receive(first, async)).IsEmpty);
        Assert.True((await Receive(second, async)).IsEmpty);
        using var unbound = Consumer(_topic.Value);
        Assert.True((await Receive(unbound, async)).IsEmpty);
        Assert.Equal(_topic, message.Header.Topic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_workers_share_a_subscriber_queue_should_compete_for_its_copy(bool async)
    {
        //Arrange
        using var first = Consumer("first");
        using var otherWorker = Consumer("first");
        using var second = Consumer("second");
        var message = CreateMessage();
        await Send(message, async);

        //Act
        var received = await Receive(first, async);
        var competing = await Receive(otherWorker, async);

        //Assert
        AssertCopy(message, received);
        Assert.True(competing.IsEmpty);
        AssertCopy(message, await Receive(second, async));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_retrying_a_subscriber_copy_should_only_requeue_to_that_subscriber(bool async)
    {
        //Arrange
        using var first = Consumer("first");
        using var second = Consumer("second");
        var message = CreateMessage();
        await Send(message, async);
        var failed = await Receive(first, async);
        AssertCopy(message, failed);
        AssertCopy(message, await Receive(second, async));

        //Act
        var accepted = async ? await first.RequeueAsync(failed) : first.Requeue(failed);

        //Assert
        Assert.True(accepted);
        AssertCopy(message, await Receive(first, async));
        Assert.True((await Receive(second, async)).IsEmpty);
        using var unbound = Consumer(_topic.Value);
        Assert.True((await Receive(unbound, async)).IsEmpty);
        Assert.Equal(_topic, failed.Header.Topic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_purging_one_subscriber_should_preserve_other_subscribers(bool async)
    {
        //Arrange
        using var first = Consumer("first");
        using var second = Consumer("second");
        var message = CreateMessage();
        await Send(message, async);

        //Act
        if (async)
            await first.PurgeAsync();
        else
            first.Purge();

        //Assert
        Assert.True((await Receive(first, async)).IsEmpty);
        AssertCopy(message, await Receive(second, async));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_one_subscriber_insert_fails_should_leave_no_partial_delivery(bool async)
    {
        //Arrange
        ExecuteSql($"ALTER TABLE [{_configuration.QueueStoreTable}] ADD CONSTRAINT [CK_{_configuration.QueueStoreTable}] CHECK (Topic <> N'second')");
        var message = CreateMessage();

        //Act
        await Assert.ThrowsAsync<SqlException>(() => Send(message, async));

        //Assert
        using var first = Consumer("first");
        using var second = Consumer("second");
        Assert.True((await Receive(first, async)).IsEmpty);
        Assert.True((await Receive(second, async)).IsEmpty);
        ExecuteSql($"ALTER TABLE [{_configuration.QueueStoreTable}] DROP CONSTRAINT [CK_{_configuration.QueueStoreTable}]");
        await Send(message, async);
        AssertCopy(message, await Receive(first, async));
        AssertCopy(message, await Receive(second, async));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_the_ambient_transaction_rolls_back_should_roll_back_every_copy(bool async)
    {
        //Arrange
        var message = CreateMessage();

        //Act
        using (ReadCommittedTransaction())
            await Send(message, async);

        //Assert
        using var first = Consumer("first");
        using var second = Consumer("second");
        Assert.True((await Receive(first, async)).IsEmpty);
        Assert.True((await Receive(second, async)).IsEmpty);

        var nextMessage = CreateMessage();
        await Send(nextMessage, async);
        AssertCopy(nextMessage, await Receive(first, async));
        AssertCopy(nextMessage, await Receive(second, async));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_the_ambient_transaction_commits_should_deliver_every_copy(bool async)
    {
        //Arrange
        var message = CreateMessage();

        //Act
        using (var transaction = ReadCommittedTransaction())
        {
            await Send(message, async);
            transaction.Complete();
        }

        //Assert
        using var first = Consumer("first");
        using var second = Consumer("second");
        AssertCopy(message, await Receive(first, async));
        AssertCopy(message, await Receive(second, async));
        Assert.True((await Receive(first, async)).IsEmpty);
        Assert.True((await Receive(second, async)).IsEmpty);
    }

    [Fact]
    public async Task When_publishing_is_cancelled_should_not_enqueue_any_copy()
    {
        //Arrange
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        //Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _producer.SendAsync(CreateMessage(), cancelled.Token));

        //Assert
        using var first = Consumer("first");
        using var second = Consumer("second");
        Assert.True((await Receive(first, true)).IsEmpty);
        Assert.True((await Receive(second, true)).IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_subscriber_names_are_equivalent_under_the_column_collation_should_enqueue_one_copy(bool async)
    {
        //Arrange
        ExecuteSql($"DROP INDEX [IX_{_configuration.QueueStoreTable}_Topic] ON [{_configuration.QueueStoreTable}]");
        ExecuteSql($"ALTER TABLE [{_configuration.QueueStoreTable}] ALTER COLUMN Topic nvarchar(255) COLLATE Latin1_General_CI_AI NOT NULL");
        _publication.SubscriberChannels = [new("café"), new("CAFE"), new("cafe ")];
        using var consumer = Consumer("café");
        var message = CreateMessage();

        //Act
        await Send(message, async);

        //Assert
        AssertCopy(message, await Receive(consumer, async));
        Assert.True((await Receive(consumer, async)).IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_subscriber_name_contains_sql_characters_should_treat_it_as_data(bool async)
    {
        //Arrange
        const string name = "subscriber'; SELECT 1; -- 雪";
        _publication.SubscriberChannels = [new(name)];
        using var consumer = Consumer(name);
        var message = CreateMessage();

        //Act
        await Send(message, async);

        //Assert
        AssertCopy(message, await Receive(consumer, async));
        Assert.True((await Receive(consumer, async)).IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_subscriber_names_differ_under_a_binary_collation_should_deliver_to_both_queues(bool async)
    {
        //Arrange
        ExecuteSql($"DROP INDEX [IX_{_configuration.QueueStoreTable}_Topic] ON [{_configuration.QueueStoreTable}]");
        ExecuteSql($"ALTER TABLE [{_configuration.QueueStoreTable}] ALTER COLUMN Topic nvarchar(255) COLLATE Latin1_General_100_BIN2 NOT NULL");
        _publication.SubscriberChannels = [new("subscriber"), new("SUBSCRIBER")];
        var message = CreateMessage();

        //Act
        await Send(message, async);

        //Assert
        using var lower = Consumer("subscriber");
        using var upper = Consumer("SUBSCRIBER");
        AssertCopy(message, await Receive(lower, async));
        AssertCopy(message, await Receive(upper, async));
        Assert.True((await Receive(lower, async)).IsEmpty);
        Assert.True((await Receive(upper, async)).IsEmpty);
    }

    [Fact]
    public async Task When_publishers_and_workers_run_concurrently_should_deliver_once_per_subscriber()
    {
        //Arrange
        var messages = Enumerable.Range(0, 200).Select(_ => CreateMessage()).ToArray();
        var deliveries = new[] { new ConcurrentBag<Message>(), new ConcurrentBag<Message>() };
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        async Task Publish(int publisher)
        {
            await start.Task;
            for (var index = publisher; index < messages.Length; index += 4)
                await _producer.SendAsync(messages[index], cancellation.Token);
        }

        async Task Consume(string queue, ConcurrentBag<Message> received)
        {
            using var worker = Consumer(queue);
            await start.Task;
            while (received.Count < messages.Length)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var message = (await worker.ReceiveAsync(s_timeout, cancellation.Token)).Single();
                if (message.IsEmpty)
                    await Task.Delay(10, cancellation.Token);
                else
                    received.Add(message);
            }
        }

        var publishers = Enumerable.Range(0, 4).Select(Publish).ToArray();
        var workers = new[] { "first", "second" }.SelectMany((queue, index) =>
            Enumerable.Range(0, 4).Select(_ => Consume(queue, deliveries[index]))).ToArray();

        //Act
        start.SetResult(true);
        await Task.WhenAll(publishers.Concat(workers));

        //Assert
        var expected = messages.ToDictionary(message => message.Id.Value);
        foreach (var received in deliveries)
        {
            Assert.Equal(expected.Keys.OrderBy(id => id), received.Select(message => message.Id.Value).OrderBy(id => id));
            Assert.All(received, message => AssertCopy(expected[message.Id.Value], message));
        }
        using var second = Consumer("second");
        using var first = Consumer("first");
        Assert.True((await Receive(first, true)).IsEmpty);
        Assert.True((await Receive(second, true)).IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_publishing_a_large_unicode_payload_to_a_maximum_length_queue_should_preserve_the_entire_message(bool async)
    {
        //Arrange
        var queue = new string('q', 253) + "🚀";
        _publication.SubscriberChannels = [new(queue), new("second")];
        var body = string.Concat(Enumerable.Repeat("雪🚀", 4000));
        var message = new Message(new MessageHeader(Id.Random(), _topic, MessageType.MT_EVENT), new MessageBody(body));

        //Act
        await Send(message, async);

        //Assert
        using var first = Consumer(queue);
        using var second = Consumer("second");
        AssertCopy(message, await Receive(first, async));
        AssertCopy(message, await Receive(second, async));
        Assert.True((await Receive(first, async)).IsEmpty);
        Assert.True((await Receive(second, async)).IsEmpty);
    }

    [Fact]
    public async Task When_using_async_factories_should_deliver_to_each_subscriber_channel()
    {
        //Arrange
        await using var producer = (IAmAMessageProducerAsync)(await new MsSqlMessageProducerFactory(
            _configuration, [_publication]).CreateAsync()).Single().Value;
        var factory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));
        using var first = await factory.CreateAsyncChannelAsync(new MsSqlSubscription<Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand>(
            new("first"), new("first"), _topic));
        using var second = factory.CreateAsyncChannel(new MsSqlSubscription<Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand>(
            new("second"), new("second"), _topic));
        var message = CreateMessage();

        //Act
        await producer.SendAsync(message);

        //Assert
        AssertCopy(message, await first.ReceiveAsync(s_timeout));
        AssertCopy(message, await second.ReceiveAsync(s_timeout));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_rejecting_one_copy_should_leave_the_other_subscriber_untouched(bool async)
    {
        //Arrange
        using var first = new MsSqlMessageConsumer(_configuration, "first", deadLetterRoutingKey: new RoutingKey("dead"));
        using var second = Consumer("second");
        using var dead = Consumer("dead");
        var message = CreateMessage();
        await Send(message, async);
        var rejected = await Receive(first, async);
        AssertCopy(message, rejected);

        //Act
        var reason = new MessageRejectionReason(RejectionReason.DeliveryError, "Failed handler");
        var accepted = async ? await first.RejectAsync(rejected, reason) : first.Reject(rejected, reason);

        //Assert
        Assert.True(accepted);
        var deadCopy = await Receive(dead, async);
        Assert.Equal(message.Id, deadCopy.Id);
        Assert.Equal("dead", deadCopy.Header.Topic.Value);
        Assert.Equal(_topic.Value, deadCopy.Header.Bag["originalTopic"].ToString());
        AssertCopy(message, await Receive(second, async));
        Assert.True((await Receive(first, async)).IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_publishing_to_the_maximum_number_of_subscribers_should_enqueue_every_copy(bool async)
    {
        //Arrange
        _publication.SubscriberChannels = Enumerable.Range(0, 1000).Select(i => new ChannelName($"subscriber{i}")).ToArray();
        var message = CreateMessage();

        //Act
        await Send(message, async);

        //Assert
        using var connection = new SqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM [{_configuration.QueueStoreTable}]";
        Assert.Equal(1000, Convert.ToInt32(command.ExecuteScalar()));
        using var first = Consumer("subscriber0");
        using var last = Consumer("subscriber999");
        AssertCopy(message, await Receive(first, async));
        AssertCopy(message, await Receive(last, async));
    }

    // TransactionScope defaults to Serializable, which can survive pooling and invalidate READPAST dequeues.
    private static TransactionScope ReadCommittedTransaction() => new(
        TransactionScopeOption.Required,
        new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
        TransactionScopeAsyncFlowOption.Enabled);

    private MsSqlMessageConsumer Consumer(string channel) =>
        (MsSqlMessageConsumer)new MsSqlMessageConsumerFactory(_configuration).Create(
            new MsSqlSubscription(new SubscriptionName(Guid.NewGuid().ToString()), new ChannelName(channel), _topic, requestType: typeof(Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand)));

    private Message CreateMessage() => new(new MessageHeader(Id.Random(), _topic, MessageType.MT_EVENT), new MessageBody("payload"));

    private Task Send(Message message, bool async)
    {
        if (async)
            return _producer.SendAsync(message);
        _producer.Send(message);
        return Task.CompletedTask;
    }

    private static async Task<Message> Receive(MsSqlMessageConsumer consumer, bool async) =>
        (async ? await consumer.ReceiveAsync(s_timeout) : consumer.Receive(TimeSpan.Zero)).Single();

    private static void AssertCopy(Message expected, Message received)
    {
        Assert.False(received.IsEmpty);
        Assert.Equal(expected.Id, received.Id);
        Assert.Equal(expected.Header.Topic, received.Header.Topic);
        Assert.Equal(expected.Body.Value, received.Body.Value);
    }

    private void ExecuteSql(string sql)
    {
        using var connection = new SqlConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _producer.Dispose();
        Configuration.DeleteTable(_configuration.ConnectionString, $"[{_configuration.QueueStoreTable}]");
    }
}
