using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.Outbox;

/// <summary>
/// Base test class that defines the causation-tracking scenarios every <see cref="IAmACausationTrackingOutbox"/>
/// implementation must satisfy. Persistent store test projects inherit this and supply their store via
/// <see cref="Outbox"/> plus the <see cref="CreateStore"/>/<see cref="DeleteStore"/> hooks.
/// </summary>
/// <typeparam name="TTransaction">The transaction type the outbox enrolls in.</typeparam>
public abstract class CausationTrackingOutboxBaseTests<TTransaction> : IDisposable
{
    protected const string CausationA = "causation-A";
    protected const string CausationB = "causation-B";

    /// <summary>
    /// The store under test. Must also implement <see cref="IAmAnOutboxAsync{TMessage, TTransaction}"/> and
    /// <see cref="IAmACausationTrackingOutbox"/>.
    /// </summary>
    protected abstract IAmAnOutboxSync<Message, TTransaction> Outbox { get; }

    /// <summary>
    /// How long outstanding-message assertions may wait for an eventually consistent index.
    /// The default requires reads to reflect writes immediately.
    /// </summary>
    protected virtual TimeSpan ReadConsistencyTimeout => TimeSpan.Zero;

    private IAmAnOutboxAsync<Message, TTransaction> OutboxAsync => (IAmAnOutboxAsync<Message, TTransaction>)Outbox;
    private IAmACausationTrackingOutbox TrackingOutbox => (IAmACausationTrackingOutbox)Outbox;

    protected CausationTrackingOutboxBaseTests()
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

    protected virtual Message CreateMessage()
    {
        return new Message(
            new MessageHeader(Id.Random(), new RoutingKey("causation_topic"), MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }

    private static RequestContext ContextWithCausation(string causationId)
    {
        var context = new RequestContext();
        context.Bag[RequestContextBagNames.CausationId] = causationId;
        return context;
    }

    [Fact]
    public void When_replaying_causation_on_outbox_should_clear_dispatch_state()
    {
        // Arrange
        var contextA = ContextWithCausation(CausationA);
        var contextB = ContextWithCausation(CausationB);
        var firstWithA = CreateMessage();
        var secondWithA = CreateMessage();
        var messageWithB = CreateMessage();

        Outbox.Add(firstWithA, contextA);
        Outbox.Add(secondWithA, contextA);
        Outbox.Add(messageWithB, contextB);

        var dispatchedAt = DateTimeOffset.UtcNow;
        Outbox.MarkDispatched(firstWithA.Id, contextA, dispatchedAt);
        Outbox.MarkDispatched(secondWithA.Id, contextA, dispatchedAt);
        Outbox.MarkDispatched(messageWithB.Id, contextB, dispatchedAt);

        // all three start dispatched, so none are outstanding
        var outstandingBefore = WaitForOutstandingMessages(contextA,
            ids => !ids.Contains(firstWithA.Id) && !ids.Contains(secondWithA.Id) && !ids.Contains(messageWithB.Id));
        Assert.DoesNotContain(firstWithA.Id, outstandingBefore);
        Assert.DoesNotContain(secondWithA.Id, outstandingBefore);
        Assert.DoesNotContain(messageWithB.Id, outstandingBefore);

        // Act
        TrackingOutbox.ReplayCausation(CausationA, contextA);

        // Assert — the two CausationA messages are outstanding again
        var outstanding = WaitForOutstandingMessages(contextA,
            ids => ids.Contains(firstWithA.Id) && ids.Contains(secondWithA.Id) && !ids.Contains(messageWithB.Id));
        Assert.Contains(firstWithA.Id, outstanding);
        Assert.Contains(secondWithA.Id, outstanding);

        // Assert — the CausationB message is untouched and still dispatched
        Assert.DoesNotContain(messageWithB.Id, outstanding);
    }

    [Fact]
    public async Task When_replaying_causation_on_outbox_should_clear_dispatch_state_async()
    {
        // Arrange
        var contextA = ContextWithCausation(CausationA);
        var contextB = ContextWithCausation(CausationB);
        var firstWithA = CreateMessage();
        var secondWithA = CreateMessage();
        var messageWithB = CreateMessage();

        await OutboxAsync.AddAsync(firstWithA, contextA);
        await OutboxAsync.AddAsync(secondWithA, contextA);
        await OutboxAsync.AddAsync(messageWithB, contextB);

        var dispatchedAt = DateTimeOffset.UtcNow;
        await OutboxAsync.MarkDispatchedAsync(firstWithA.Id, contextA, dispatchedAt);
        await OutboxAsync.MarkDispatchedAsync(secondWithA.Id, contextA, dispatchedAt);
        await OutboxAsync.MarkDispatchedAsync(messageWithB.Id, contextB, dispatchedAt);

        // all three start dispatched, so none are outstanding
        var outstandingBefore = await WaitForOutstandingMessagesAsync(contextA,
            ids => !ids.Contains(firstWithA.Id) && !ids.Contains(secondWithA.Id) && !ids.Contains(messageWithB.Id));
        Assert.DoesNotContain(firstWithA.Id, outstandingBefore);
        Assert.DoesNotContain(secondWithA.Id, outstandingBefore);
        Assert.DoesNotContain(messageWithB.Id, outstandingBefore);

        // Act
        await TrackingOutbox.ReplayCausationAsync(CausationA, contextA);

        // Assert — the two CausationA messages are outstanding again
        var outstanding = await WaitForOutstandingMessagesAsync(contextA,
            ids => ids.Contains(firstWithA.Id) && ids.Contains(secondWithA.Id) && !ids.Contains(messageWithB.Id));
        Assert.Contains(firstWithA.Id, outstanding);
        Assert.Contains(secondWithA.Id, outstanding);

        // Assert — the CausationB message is untouched and still dispatched
        Assert.DoesNotContain(messageWithB.Id, outstanding);
    }

    [Fact]
    public void When_replaying_causation_for_messages_deposited_in_bulk_should_clear_dispatch_state()
    {
        // Arrange — deposit the CausationA messages through the bulk Add(IEnumerable) overload
        var contextA = ContextWithCausation(CausationA);
        var contextB = ContextWithCausation(CausationB);
        var firstWithA = CreateMessage();
        var secondWithA = CreateMessage();
        var messageWithB = CreateMessage();

        Outbox.Add(new[] { firstWithA, secondWithA }, contextA);
        Outbox.Add(new[] { messageWithB }, contextB);

        var dispatchedAt = DateTimeOffset.UtcNow;
        Outbox.MarkDispatched(firstWithA.Id, contextA, dispatchedAt);
        Outbox.MarkDispatched(secondWithA.Id, contextA, dispatchedAt);
        Outbox.MarkDispatched(messageWithB.Id, contextB, dispatchedAt);

        // all three start dispatched, so none are outstanding
        var outstandingBefore = WaitForOutstandingMessages(contextA,
            ids => !ids.Contains(firstWithA.Id) && !ids.Contains(secondWithA.Id) && !ids.Contains(messageWithB.Id));
        Assert.DoesNotContain(firstWithA.Id, outstandingBefore);
        Assert.DoesNotContain(secondWithA.Id, outstandingBefore);
        Assert.DoesNotContain(messageWithB.Id, outstandingBefore);

        // Act
        TrackingOutbox.ReplayCausation(CausationA, contextA);

        // Assert — the two bulk-deposited CausationA messages are outstanding again
        var outstanding = WaitForOutstandingMessages(contextA,
            ids => ids.Contains(firstWithA.Id) && ids.Contains(secondWithA.Id) && !ids.Contains(messageWithB.Id));
        Assert.Contains(firstWithA.Id, outstanding);
        Assert.Contains(secondWithA.Id, outstanding);

        // Assert — the CausationB message is untouched and still dispatched
        Assert.DoesNotContain(messageWithB.Id, outstanding);
    }

    [Fact]
    public async Task When_replaying_causation_for_messages_deposited_in_bulk_should_clear_dispatch_state_async()
    {
        // Arrange — deposit the CausationA messages through the bulk AddAsync(IEnumerable) overload
        var contextA = ContextWithCausation(CausationA);
        var contextB = ContextWithCausation(CausationB);
        var firstWithA = CreateMessage();
        var secondWithA = CreateMessage();
        var messageWithB = CreateMessage();

        await OutboxAsync.AddAsync(new[] { firstWithA, secondWithA }, contextA);
        await OutboxAsync.AddAsync(new[] { messageWithB }, contextB);

        var dispatchedAt = DateTimeOffset.UtcNow;
        await OutboxAsync.MarkDispatchedAsync(firstWithA.Id, contextA, dispatchedAt);
        await OutboxAsync.MarkDispatchedAsync(secondWithA.Id, contextA, dispatchedAt);
        await OutboxAsync.MarkDispatchedAsync(messageWithB.Id, contextB, dispatchedAt);

        // all three start dispatched, so none are outstanding
        var outstandingBefore = await WaitForOutstandingMessagesAsync(contextA,
            ids => !ids.Contains(firstWithA.Id) && !ids.Contains(secondWithA.Id) && !ids.Contains(messageWithB.Id));
        Assert.DoesNotContain(firstWithA.Id, outstandingBefore);
        Assert.DoesNotContain(secondWithA.Id, outstandingBefore);
        Assert.DoesNotContain(messageWithB.Id, outstandingBefore);

        // Act
        await TrackingOutbox.ReplayCausationAsync(CausationA, contextA);

        // Assert — the two bulk-deposited CausationA messages are outstanding again
        var outstanding = await WaitForOutstandingMessagesAsync(contextA,
            ids => ids.Contains(firstWithA.Id) && ids.Contains(secondWithA.Id) && !ids.Contains(messageWithB.Id));
        Assert.Contains(firstWithA.Id, outstanding);
        Assert.Contains(secondWithA.Id, outstanding);

        // Assert — the CausationB message is untouched and still dispatched
        Assert.DoesNotContain(messageWithB.Id, outstanding);
    }

    [Fact]
    public void When_asking_outbox_if_it_supports_causation_tracking_should_be_true()
    {
        // Act
        var supportsCausationTracking = TrackingOutbox.SupportsCausationTracking();

        // Assert
        Assert.True(supportsCausationTracking);
    }

    private Id[] WaitForOutstandingMessages(RequestContext context, Func<Id[], bool> expectedState)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var outstanding = Outbox.OutstandingMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray();
            if (expectedState(outstanding) || stopwatch.Elapsed >= ReadConsistencyTimeout)
                return outstanding;

            var remaining = ReadConsistencyTimeout - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                Thread.Sleep(remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50));
        }
    }

    private async Task<Id[]> WaitForOutstandingMessagesAsync(RequestContext context, Func<Id[], bool> expectedState)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var outstanding = (await OutboxAsync.OutstandingMessagesAsync(TimeSpan.Zero, context))
                .Select(m => m.Id).ToArray();
            if (expectedState(outstanding) || stopwatch.Elapsed >= ReadConsistencyTimeout)
                return outstanding;

            var remaining = ReadConsistencyTimeout - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50));
        }
    }
}
