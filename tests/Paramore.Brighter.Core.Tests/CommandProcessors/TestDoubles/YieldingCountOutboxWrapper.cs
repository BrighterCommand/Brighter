using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Archiving.TestDoubles;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

/// <summary>
/// An async-only outbox whose outstanding count yields before it answers, so that it completes on a later
/// continuation instead of synchronously like the InMemoryOutbox does.
/// </summary>
public class YieldingCountOutboxWrapper(InMemoryOutbox inner) : AsyncOnlyOutboxWrapper(inner)
{
    public override async Task<int> GetOutstandingMessageCountAsync(TimeSpan dispatchedSince,
        RequestContext requestContext, int maxCount = 100, Dictionary<string, object> args = null,
        CancellationToken cancellationToken = default)
    {
        // Task.Yield posts its continuation to the ambient SynchronizationContext, if there is one
        await Task.Yield();

        return await base.GetOutstandingMessageCountAsync(dispatchedSince, requestContext, maxCount, args,
            cancellationToken);
    }
}
