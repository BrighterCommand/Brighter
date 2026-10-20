using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="IAmAnOutboxProducerMediator"/> whose sweeps complete or throw, as the test switches it between sweeps.
/// </summary>
public sealed class SwitchableOutboxProducerMediator : SweepOnlyOutboxProducerMediator
{
    private string? _failure;

    /// <summary>Makes later sweeps complete.</summary>
    public void Complete() => Volatile.Write(ref _failure, null);

    /// <summary>Makes later sweeps throw an <see cref="InvalidOperationException"/> with <paramref name="message"/>.</summary>
    public void Throw(string message = "the sweep failed") => Volatile.Write(ref _failure, message);

    public override Task ClearOutstandingFromOutboxAsync(int amountToClear,
        TimeSpan minimumAge,
        bool useBulk,
        RequestContext requestContext,
        Dictionary<string, object>? args = null,
        CancellationToken cancellationToken = default)
    {
        var failure = Volatile.Read(ref _failure);
        return failure is null ? Task.CompletedTask : throw new InvalidOperationException(failure);
    }
}
