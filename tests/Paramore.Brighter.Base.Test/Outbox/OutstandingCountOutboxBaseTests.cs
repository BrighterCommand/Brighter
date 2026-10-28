using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.Outbox;

/// <summary>
/// Base test class that defines the overall outstanding-count cases every outbox must satisfy. It declares no facts,
/// because a gap mark acts on one case of one outbox: the test classes that derive from it declare one fact for each
/// case and call the matching Assert method.
/// </summary>
/// <typeparam name="TTransaction">The transaction type the outbox enrolls in.</typeparam>
public abstract class OutstandingCountOutboxBaseTests<TTransaction> : IDisposable
{
    private static readonly TimeSpan s_dispatchedSince = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The outbox under test, over a store that no other test writes to. The cases call the asynchronous count.
    /// </summary>
    protected abstract IAmAnOutboxAsync<Message, TTransaction> Outbox { get; }

    /// <summary>
    /// How long a case may retry a count against an eventually consistent index.
    /// The default requires reads to reflect writes immediately.
    /// </summary>
    protected virtual TimeSpan ReadConsistencyTimeout => TimeSpan.Zero;

    protected OutstandingCountOutboxBaseTests()
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
    /// Adds the message, undispatched, so that it is at least <paramref name="age"/> old on the outbox's own clock
    /// when the case counts. It is older only by the time the case takes to reach the count.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <param name="age">How old the message is when the case counts.</param>
    protected abstract Task AddMessageWithAgeAsync(Message message, TimeSpan age);

    /// <summary>
    /// Readies the outbox's clock for the count. A case calls it once, after the adds and before the first count.
    /// It does nothing by default.
    /// </summary>
    protected virtual void BeforeCount()
    {
    }

    protected virtual Message CreateMessage()
    {
        return new Message(
            new MessageHeader(Id.Random(), new RoutingKey($"outstanding-count-{Uuid.New():N}"), MessageType.MT_DOCUMENT),
            new MessageBody("message body"));
    }

    /// <summary>
    /// Counts the nine messages with an age of five minutes and a ceiling of 100, and asserts five.
    /// </summary>
    protected async Task AssertAgeAsync()
    {
        await AddAc16MessagesAsync();
        BeforeCount();

        var count = await WaitForCountAsync(100, 5);

        Assert.Equal(5, count);
    }

    /// <summary>
    /// Counts the nine messages with an age of five minutes and a ceiling of three, and asserts three.
    /// </summary>
    protected async Task AssertCeilingOfThreeAsync()
    {
        await AddAc16MessagesAsync();
        BeforeCount();

        var count = await WaitForCountAsync(3, 3);

        Assert.Equal(3, count);
    }

    /// <summary>
    /// Counts the nine messages with an age of five minutes and a ceiling of five, and asserts five.
    /// </summary>
    protected async Task AssertCeilingOfFiveAsync()
    {
        await AddAc16MessagesAsync();
        BeforeCount();

        var count = await WaitForCountAsync(5, 5);

        Assert.Equal(5, count);
    }

    /// <summary>
    /// Counts the nine messages once, with an age of five minutes, and returns the result, or the exception,
    /// as text. It asserts nothing.
    /// </summary>
    /// <param name="maxCount">The ceiling for the count.</param>
    /// <returns>The text to write to the test output.</returns>
    protected async Task<string> ObserveCountAsync(int maxCount)
    {
        await AddAc16MessagesAsync();
        BeforeCount();

        try
        {
            var count = await Outbox.GetOutstandingMessageCountAsync(s_dispatchedSince, new RequestContext(), maxCount);
            return $"Count with a ceiling of {maxCount}: {count}";
        }
        catch (Exception exception)
        {
            return $"Count with a ceiling of {maxCount} threw {exception.GetType().FullName}: {exception.Message}";
        }
    }

    // Five undispatched messages and two dispatched ones, all ten minutes old, then two undispatched ones that
    // are one minute old, oldest first.
    private async Task AddAc16MessagesAsync()
    {
        var tenMinutes = TimeSpan.FromMinutes(10);
        var dispatched = new Message[2];

        for (var i = 0; i < 5; i++)
            await AddMessageWithAgeAsync(CreateMessage(), tenMinutes);

        for (var i = 0; i < dispatched.Length; i++)
        {
            dispatched[i] = CreateMessage();
            await AddMessageWithAgeAsync(dispatched[i], tenMinutes);
        }

        for (var i = 0; i < 2; i++)
            await AddMessageWithAgeAsync(CreateMessage(), TimeSpan.FromMinutes(1));

        foreach (var message in dispatched)
            await Outbox.MarkDispatchedAsync(message.Id, new RequestContext());
    }

    private async Task<int> WaitForCountAsync(int maxCount, int expected)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var count = await Outbox.GetOutstandingMessageCountAsync(s_dispatchedSince, new RequestContext(), maxCount);
            if (count == expected || stopwatch.Elapsed >= ReadConsistencyTimeout)
                return count;

            var remaining = ReadConsistencyTimeout - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50));
        }
    }
}
