using System;
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
    public OutstandingCountCancellationSetup(
        IAmAnOutboxAsync<Message, TTransaction> outbox,
        bool storeHeld,
        Type clientCancellationException,
        Func<ValueTask> release)
    {
        Outbox = outbox;
        StoreHeld = storeHeld;
        ClientCancellationException = clientCancellationException;
        _release = release;
    }

    public IAmAnOutboxAsync<Message, TTransaction> Outbox { get; }

    public bool StoreHeld { get; }

    public Type ClientCancellationException { get; }

    /// <summary>
    /// Releases the hold and removes the store. It never throws.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _release();
        }
        catch (Exception)
        {
            // Releasing is best effort: a failure here must not hide the case's own result.
        }
    }
}
