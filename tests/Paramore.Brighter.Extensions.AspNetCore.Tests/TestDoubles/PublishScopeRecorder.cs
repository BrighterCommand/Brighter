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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records <see cref="PublishOrderController"/>'s own <see cref="IOrderDbContext"/> and the ones
/// <see cref="PublishScopeSubscriberOne"/>/<see cref="PublishScopeSubscriberTwo"/> resolved, so a test can
/// assert neither subscriber adopted the controller's instance. Register as a singleton.
/// </summary>
/// <remarks>
/// <see cref="RecordSubscriberInstance"/> may be called from more than one subscriber concurrently (ADR
/// 0013 - <c>Publish</c> invokes subscribers in parallel), so subscriber instances are held in a
/// <see cref="ConcurrentBag{T}"/>.
/// </remarks>
public sealed class PublishScopeRecorder
{
    private readonly ConcurrentBag<IOrderDbContext> _subscriberInstances = new();

    /// <summary>
    /// The controller's own <see cref="IOrderDbContext"/> - the request-scoped instance a subscriber
    /// must not adopt.
    /// </summary>
    public IOrderDbContext? RequestScopeInstance { get; private set; }

    /// <summary>
    /// The <see cref="IOrderDbContext"/> each subscriber resolved for itself.
    /// </summary>
    public IReadOnlyCollection<IOrderDbContext> SubscriberInstances => _subscriberInstances;

    /// <summary>
    /// The request-scoped instance's own dispose count, captured immediately after <c>PublishAsync</c>
    /// returned - before the HTTP response, and therefore before ASP.NET could have disposed the request
    /// scope.
    /// </summary>
    public int RequestScopeDisposeCountAfterPublish { get; private set; }

    /// <summary>
    /// Each subscriber instance's own dispose count, captured at the same point.
    /// </summary>
    public IReadOnlyList<int> SubscriberDisposeCountsAfterPublish { get; private set; } = new List<int>();

    /// <summary>
    /// Records the controller's own <see cref="IOrderDbContext"/>.
    /// </summary>
    public void RecordRequestScopeInstance(IOrderDbContext context) => RequestScopeInstance = context;

    /// <summary>
    /// Records one subscriber's own <see cref="IOrderDbContext"/>.
    /// </summary>
    public void RecordSubscriberInstance(IOrderDbContext context) => _subscriberInstances.Add(context);

    /// <summary>
    /// Captures every instance's current dispose count. Call immediately after <c>PublishAsync</c>
    /// returns, before the HTTP response is sent.
    /// </summary>
    public void CaptureDisposeCountsAfterPublish()
    {
        RequestScopeDisposeCountAfterPublish = RequestScopeInstance!.DisposeCount;
        SubscriberDisposeCountsAfterPublish = _subscriberInstances.Select(instance => instance.DisposeCount).ToList();
    }
}
