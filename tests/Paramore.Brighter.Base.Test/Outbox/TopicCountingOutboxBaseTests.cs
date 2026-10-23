using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.Outbox;

/// <summary>
/// Base test class that defines the per-topic stale-count scenarios every <see cref="IAmATopicCountingOutbox"/>
/// implementation must satisfy. Store test projects inherit this and supply their store via
/// <see cref="Outbox"/>, the <see cref="CreateStore"/>/<see cref="DeleteStore"/> hooks and
/// <see cref="AddMessageWithAgeAsync"/>.
/// </summary>
/// <typeparam name="TTransaction">The transaction type the outbox enrolls in.</typeparam>
public abstract class TopicCountingOutboxBaseTests<TTransaction> : IDisposable
{
    // Separate test processes can share a store, so each scenario counts its own topics.
    protected RoutingKey Orders { get; } = new($"orders-{Uuid.New():N}");
    protected RoutingKey Payments { get; } = new($"payments-{Uuid.New():N}");
    protected RoutingKey Refunds { get; } = new($"refunds-{Uuid.New():N}");
    protected RoutingKey Audit { get; } = new($"audit-{Uuid.New():N}");

    /// <summary>
    /// The store under test. Must also implement <see cref="IAmAnOutboxAsync{TMessage, TTransaction}"/> and
    /// <see cref="IAmATopicCountingOutbox"/>.
    /// </summary>
    protected abstract IAmAnOutboxSync<Message, TTransaction> Outbox { get; }

    /// <summary>
    /// How long a count may be retried against an eventually consistent index.
    /// The default requires reads to reflect writes immediately.
    /// </summary>
    protected virtual TimeSpan ReadConsistencyTimeout => TimeSpan.Zero;

    private IAmAnOutboxAsync<Message, TTransaction> OutboxAsync => (IAmAnOutboxAsync<Message, TTransaction>)Outbox;
    private IAmATopicCountingOutbox TopicCountingOutbox => (IAmATopicCountingOutbox)Outbox;

    protected TopicCountingOutboxBaseTests()
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        BeforeEachTest();
    }

    protected virtual void BeforeEachTest()
    {
        CreateStore();
    }

    protected virtual void CreateStore()
    {
    }

    public void Dispose()
    {
        AfterEachTest();
    }

    protected virtual void AfterEachTest()
    {
        DeleteStore();
    }

    protected virtual void DeleteStore()
    {
    }

    /// <summary>
    /// Adds the message so that it is <paramref name="age"/> old on the outbox's own clock at the moment the case
    /// calls the capability, not only when the message is added. Cases add their messages oldest first, and all of
    /// them before their first count.
    /// </summary>
    /// <remarks>
    /// A store whose clock keeps moving can only set the age relative to that clock, so the cases keep each
    /// message's age well away from every requested topic age.
    /// </remarks>
    /// <param name="message">The message to add.</param>
    /// <param name="age">How old the message is when the case counts.</param>
    protected abstract Task AddMessageWithAgeAsync(Message message, TimeSpan age);

    /// <summary>
    /// Readies the outbox's clock for a count. A case calls it immediately before each call to the capability.
    /// It does nothing by default.
    /// </summary>
    protected virtual void BeforeCount()
    {
    }

    protected virtual Message CreateMessage(RoutingKey topic)
    {
        return new Message(
            new MessageHeader(Id.Random(), topic, MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }

    [Fact]
    public async Task When_counting_by_topic_should_return_each_topic_stale_count_capped_at_the_ceiling()
    {
        // Arrange
        await AddTopicStoreAsync();
        var request = new Dictionary<RoutingKey, TimeSpan>
        {
            [Orders] = TimeSpan.FromMinutes(5),
            [Payments] = TimeSpan.FromMinutes(2),
            [Refunds] = TimeSpan.FromMinutes(5)
        };

        // Act
        var counts = await WaitForCountsByTopicAsync(request, 100,
            result => result.TryGetValue(Orders, out var orders) && orders == 5);
        var cappedCounts = await WaitForCountsByTopicAsync(request, 3,
            result => result.TryGetValue(Orders, out var orders) && orders == 3);
        request[Orders] = TimeSpan.FromSeconds(30);
        var youngerCounts = await WaitForCountsByTopicAsync(request, 100,
            result => result.TryGetValue(Orders, out var orders) && orders == 7);

        // Assert
        Assert.Equal(5, counts[Orders]);
        Assert.Equal(1, counts[Payments]);
        Assert.Equal(0, counts[Refunds]);

        Assert.Equal(3, cappedCounts[Orders]);
        Assert.Equal(1, cappedCounts[Payments]);
        Assert.Equal(0, cappedCounts[Refunds]);

        Assert.Equal(7, youngerCounts[Orders]);
    }

    [Fact]
    public async Task When_counting_by_topic_should_answer_exactly_the_requested_topics()
    {
        // Arrange
        await AddTopicStoreAsync();
        var request = new Dictionary<RoutingKey, TimeSpan>
        {
            [Orders] = TimeSpan.FromMinutes(5),
            [Payments] = TimeSpan.FromMinutes(2),
            [Refunds] = TimeSpan.FromMinutes(5)
        };

        // Act
        var counts = await WaitForCountsByTopicAsync(request, 100,
            result => result.TryGetValue(Orders, out var orders) && orders == 5);

        // Assert
        Assert.Equal(new HashSet<RoutingKey> { Orders, Payments, Refunds }, counts.Keys.ToHashSet());
        Assert.Equal(0, counts[Refunds]);
        Assert.False(counts.ContainsKey(Audit));
    }

    [Fact]
    public async Task When_counting_by_topic_with_a_topic_in_args_should_not_narrow_the_topics()
    {
        // Arrange
        await AddTopicStoreAsync();
        var request = new Dictionary<RoutingKey, TimeSpan>
        {
            [Orders] = TimeSpan.FromMinutes(5),
            [Payments] = TimeSpan.FromMinutes(2),
            [Refunds] = TimeSpan.FromMinutes(5)
        };
        var args = new Dictionary<string, object> { ["Topic"] = Orders.Value };

        // Act
        var counts = await WaitForCountsByTopicAsync(request, 100,
            result => result.TryGetValue(Orders, out var orders) && orders == 5, args);

        // Assert
        IReadOnlyDictionary<RoutingKey, int> expected = new Dictionary<RoutingKey, int>
        {
            [Orders] = 5,
            [Payments] = 1,
            [Refunds] = 0
        };
        Assert.Equal(expected, counts);

        Assert.Equal(new Dictionary<string, object> { ["Topic"] = Orders.Value }, args);
    }

    [Fact]
    public async Task When_counting_by_topic_should_count_a_message_toward_its_header_topic()
    {
        // Arrange
        var ordersV2 = new RoutingKey($"{Orders.Value}.v2");
        var message = CreateMessage(ordersV2);
        message.Header.Bag[Message.ProducerTopicHeaderName] = Orders.Value;
        await AddMessageWithAgeAsync(message, TimeSpan.FromMinutes(10));
        var request = new Dictionary<RoutingKey, TimeSpan>
        {
            [Orders] = TimeSpan.FromMinutes(5),
            [ordersV2] = TimeSpan.FromMinutes(5)
        };

        // Act
        var counts = await WaitForCountsByTopicAsync(request, 100,
            result => result.TryGetValue(ordersV2, out var v2) && v2 == 1);

        // Assert
        IReadOnlyDictionary<RoutingKey, int> expected = new Dictionary<RoutingKey, int>
        {
            [Orders] = 0,
            [ordersV2] = 1
        };
        Assert.Equal(expected, counts);
    }

    [Fact]
    public async Task When_counting_by_topic_with_no_topics_should_return_an_empty_result()
    {
        // Arrange
        await AddTopicStoreAsync();
        var request = new Dictionary<RoutingKey, TimeSpan>();

        // Act
        var counts = await WaitForCountsByTopicAsync(request, 100, result => result.Count == 0);

        // Assert
        Assert.NotNull(counts);
        Assert.Empty(counts);
    }

    private async Task AddTopicStoreAsync()
    {
        var tenMinutes = TimeSpan.FromMinutes(10);
        var dispatched = new List<Message>();

        for (var i = 0; i < 5; i++)
            await AddMessageWithAgeAsync(CreateMessage(Orders), tenMinutes);

        for (var i = 0; i < 2; i++)
        {
            var message = CreateMessage(Orders);
            await AddMessageWithAgeAsync(message, tenMinutes);
            dispatched.Add(message);
        }

        for (var i = 0; i < 4; i++)
            await AddMessageWithAgeAsync(CreateMessage(Audit), tenMinutes);

        await AddMessageWithAgeAsync(CreateMessage(Payments), TimeSpan.FromMinutes(3));

        for (var i = 0; i < 2; i++)
            await AddMessageWithAgeAsync(CreateMessage(Orders), TimeSpan.FromMinutes(1));

        foreach (var message in dispatched)
            await OutboxAsync.MarkDispatchedAsync(message.Id, new RequestContext());
    }

    private async Task<IReadOnlyDictionary<RoutingKey, int>> WaitForCountsByTopicAsync(
        IReadOnlyDictionary<RoutingKey, TimeSpan> dispatchedSinceByTopic,
        int maxCountPerTopic,
        Func<IReadOnlyDictionary<RoutingKey, int>, bool> expectedState,
        Dictionary<string, object> args = null)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            BeforeCount();
            var counts = await TopicCountingOutbox.GetOutstandingMessageCountsByTopicAsync(
                dispatchedSinceByTopic, new RequestContext(), maxCountPerTopic, args);
            if (expectedState(counts) || stopwatch.Elapsed >= ReadConsistencyTimeout)
                return counts;

            var remaining = ReadConsistencyTimeout - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50));
        }
    }
}
