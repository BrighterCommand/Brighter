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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// The first of two subscribers to <see cref="ConcurrentPublishOrderPlaced"/> (AC-12). Records the
/// <see cref="IOrderDbContext"/> it was constructed with, arrives at the event's own
/// <see cref="ConcurrentPublishRendezvous"/> and waits for the second subscriber to arrive too, then
/// issues a nested <c>SendAsync</c> for <see cref="ConcurrentPublishInnerCommand"/> from inside its own
/// <c>HandleAsync</c> - after it has already awaited past its own resolution.
/// </summary>
public sealed class ConcurrentPublishSubscriberOne : RequestHandlerAsync<ConcurrentPublishOrderPlaced>
{
    private readonly IAmACommandProcessor _commandProcessor;
    private readonly ConcurrentPublishRecorder _recorder;

    public ConcurrentPublishSubscriberOne(
        IOrderDbContext orderDbContext, IAmACommandProcessor commandProcessor, ConcurrentPublishRecorder recorder)
    {
        _commandProcessor = commandProcessor;
        _recorder = recorder;
        _recorder.RecordSubscriberOneInstance(orderDbContext);
    }

    public override async Task<ConcurrentPublishOrderPlaced> HandleAsync(
        ConcurrentPublishOrderPlaced @event, CancellationToken cancellationToken = default)
    {
        await @event.Rendezvous.SubscriberOneArrivedAndWaitForTwoAsync(TimeSpan.FromSeconds(5));
        _recorder.RecordSubscriberOneObservedOverlap();

        await _commandProcessor.SendAsync(new ConcurrentPublishInnerCommand(), cancellationToken: cancellationToken);

        return await base.HandleAsync(@event, cancellationToken);
    }
}
