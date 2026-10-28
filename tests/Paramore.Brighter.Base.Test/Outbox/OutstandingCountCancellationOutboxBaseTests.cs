using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.Outbox;

/// <summary>
/// Base test class for the cancellation case of the overall outstanding count. It declares no fact: the test class
/// that derives from it declares the fact and calls <see cref="AssertCancellationEndsTheCountAsync"/>.
/// </summary>
/// <typeparam name="TTransaction">The transaction type the outbox enrolls in.</typeparam>
public abstract class OutstandingCountCancellationOutboxBaseTests<TTransaction>
{
    private static readonly TimeSpan s_holdFor = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_cancelAfter = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_mustEndWithin = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan s_margin = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Prepares an outbox over a store of its own that no other test writes to, held for <paramref name="holdFor"/>
    /// when the store can be held.
    /// </summary>
    /// <param name="holdFor">How long to hold the store, at least 30 seconds.</param>
    protected abstract Task<OutstandingCountCancellationSetup<TTransaction>> PrepareStoreAsync(TimeSpan holdFor);

    /// <summary>
    /// Counts against a held store, cancels the token one second later, and asserts that the count ended with
    /// <see cref="OperationCanceledException"/>, or with the client's exception, within five seconds of the
    /// cancellation. When the store cannot be held, the token is cancelled before the call.
    /// </summary>
    protected async Task AssertCancellationEndsTheCountAsync()
    {
        var setup = await PrepareStoreAsync(s_holdFor);
        using var cancellation = new CancellationTokenSource();
        Task<int> count = null;
        try
        {
            if (!setup.StoreHeld)
                cancellation.Cancel();

            // Run keeps a count that runs synchronously from blocking this thread.
            count = Task.Run(() => setup.Outbox.GetOutstandingMessageCountAsync(
                TimeSpan.FromMinutes(5), new RequestContext(), 100, null, cancellation.Token));

            if (setup.StoreHeld)
            {
                await Task.Delay(s_cancelAfter);
                cancellation.Cancel();
            }

            var cancelledAt = Stopwatch.StartNew();
            var finished = await Task.WhenAny(count, Task.Delay(s_mustEndWithin + s_margin));
            cancelledAt.Stop();

            Assert.True(ReferenceEquals(finished, count),
                $"The count was still running {cancelledAt.Elapsed.TotalSeconds:F1} seconds after the cancellation");
            Assert.True(cancelledAt.Elapsed <= s_mustEndWithin,
                $"The count ended {cancelledAt.Elapsed.TotalSeconds:F1} seconds after the cancellation");

            var exception = await Record.ExceptionAsync(() => count);
            Assert.NotNull(exception);
            Assert.True(
                exception is OperationCanceledException
                || (setup.ClientCancellationException != null && setup.ClientCancellationException.IsInstanceOfType(exception)),
                $"The count ended with {exception.GetType().FullName}: {exception.Message}");
        }
        finally
        {
            await setup.DisposeAsync();
            if (count != null)
                await Task.WhenAny(count, Task.Delay(s_mustEndWithin + s_margin));
        }
    }
}
