using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Testing;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

/// <summary>
/// Stands in for a handler that clears the outbox while handling a command, as <c>PostAsync</c> does after it
/// deposits a message: it calls the mediator's <c>ClearOutboxAsync</c> and records the threads it ran on.
/// </summary>
internal sealed class OutboxClearingCommandProcessor(IAmAnOutboxProducerMediator mediator, Id outstanding) : SpyCommandProcessor
{
    private readonly ManualResetEventSlim _handled = new(false);

    public int HandlerThread { get; private set; }
    public int ThreadAfterClear { get; private set; }

    public bool WaitForHandler(System.TimeSpan timeout) => _handled.Wait(timeout);

    public override async Task SendAsync<T>(
        T command,
        RequestContext? requestContext = null,
        bool continueOnCapturedContext = true,
        CancellationToken cancellationToken = default)
    {
        HandlerThread = System.Environment.CurrentManagedThreadId;
        await mediator.ClearOutboxAsync([outstanding], new RequestContext(), continueOnCapturedContext, cancellationToken: cancellationToken);
        ThreadAfterClear = System.Environment.CurrentManagedThreadId;
        _handled.Set();
    }
}
