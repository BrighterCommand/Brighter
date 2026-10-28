using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Base.Test.Outbox;

/// <summary>
/// What <see cref="OutstandingCountCancellationOutboxBaseTests{TTransaction}"/> runs against: an outbox over a store
/// of its own, whether the store is held, and how to let it go.
/// </summary>
/// <typeparam name="TTransaction">The transaction type the outbox enrolls in.</typeparam>
public sealed class OutstandingCountCancellationSetup<TTransaction> : IAsyncDisposable
{
    private readonly Func<ValueTask> _release;
    private int _released;

    /// <summary>
    /// Creates the setup.
    /// </summary>
    /// <param name="outbox">The asynchronous outbox under test.</param>
    /// <param name="storeHeld">Whether the store is held, so that a count blocks.</param>
    /// <param name="clientCancellationException">
    /// The exception the store's client throws when it is cancelled, or null when it throws
    /// <see cref="OperationCanceledException"/>.
    /// </param>
    /// <param name="release">Releases the hold and then removes the store.</param>
    /// <param name="holdFor">
    /// How long a held store stays held. When given, the setup releases the store on its own after that time, if
    /// it has not been disposed by then.
    /// </param>
    public OutstandingCountCancellationSetup(
        IAmAnOutboxAsync<Message, TTransaction> outbox,
        bool storeHeld,
        Type clientCancellationException,
        Func<ValueTask> release,
        TimeSpan? holdFor = null)
    {
        Outbox = outbox;
        StoreHeld = storeHeld;
        ClientCancellationException = clientCancellationException;
        _release = release;

        if (holdFor.HasValue)
        {
            _ = Task.Delay(holdFor.Value).ContinueWith(
                _ => DisposeAsync().AsTask(), TaskScheduler.Default);
        }
    }

    public IAmAnOutboxAsync<Message, TTransaction> Outbox { get; }

    public bool StoreHeld { get; }

    public Type ClientCancellationException { get; }

    /// <summary>
    /// Releases the hold and removes the store, once. It never throws.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) == 1)
        {
            return;
        }

        try
        {
            await _release();
        }
        catch (Exception)
        {
        }
    }
}
