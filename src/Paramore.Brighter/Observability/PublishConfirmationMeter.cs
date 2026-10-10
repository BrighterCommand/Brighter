#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.Observability;

/// <summary>
/// Reports how many publish confirmations are waiting to be raised, for each producer that raises them through a
/// <see cref="BatchedCallbackQueue"/>. A growing depth means confirmations arrive faster than their handlers, such
/// as marking a message dispatched in an outbox, finish; left alone, messages stay unmarked and are swept again.
/// </summary>
public sealed class PublishConfirmationMeter
{
    /// <summary>
    /// Creates the queue-depth instrument on the Brighter meter.
    /// </summary>
    /// <param name="meterFactory">Creates the <see cref="BrighterSemanticConventions.MeterName"/> meter.</param>
    public PublishConfirmationMeter(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(BrighterSemanticConventions.MeterName);

        meter.CreateObservableUpDownCounter(
            name: BrighterSemanticConventions.PublishConfirmationQueueDepth,
            observeValues: ObserveQueueDepths,
            unit: "{confirmation}",
            description: "Number of publish confirmations queued or running, by producer.");
    }

    private static IEnumerable<Measurement<long>> ObserveQueueDepths()
    {
        foreach (var queue in BatchedCallbackQueueRegistry.LiveQueues())
        {
            if (queue.MessagingSystem is not { } system || queue.Destination is null) continue;

            yield return new Measurement<long>(
                queue.Depth,
                new KeyValuePair<string, object?>(BrighterSemanticConventions.MessagingSystem, system.ToMessagingSystemName()),
                new KeyValuePair<string, object?>(BrighterSemanticConventions.MessagingDestination, queue.Destination.Value));
        }
    }
}
