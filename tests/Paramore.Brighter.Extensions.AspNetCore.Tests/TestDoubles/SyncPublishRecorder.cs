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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records every <see cref="IOrderDbContext"/> resolved during AC-39's synchronous-<c>Publish</c>
/// scenario - the controller's own request-scope instance, each of the three subscribers' own instance
/// keyed by its own marker (never by the order <c>Parallel.ForEach</c> happened to run them in), the
/// instance each nesting subscriber's own nested <c>Send</c> resolved (also keyed by that subscriber's
/// marker), and the instances a <c>Send</c>/<c>Post</c> issued after the publish returned resolved.
/// Register as a singleton in the container under test.
/// </summary>
/// <remarks>
/// <see cref="RecordSubscriberInstance"/> and <see cref="RecordNestedSendInstance"/> may be called from
/// more than one subscriber concurrently (<c>Publish</c> dispatches via <c>Parallel.ForEach</c>), so both
/// are held in a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by each subscriber's own marker.
/// </remarks>
public sealed class SyncPublishRecorder
{
    private readonly ConcurrentDictionary<string, IOrderDbContext> _subscriberInstances = new();
    private readonly ConcurrentDictionary<string, IOrderDbContext> _nestedSendInstances = new();

    /// <summary>
    /// The controller's own <see cref="IOrderDbContext"/> - the request-scoped instance <c>R</c>.
    /// </summary>
    public IOrderDbContext? RequestScopeInstance { get; private set; }

    /// <summary>
    /// Each subscriber's own <see cref="IOrderDbContext"/>, keyed by that subscriber's own marker.
    /// </summary>
    public IReadOnlyDictionary<string, IOrderDbContext> SubscriberInstances => _subscriberInstances;

    /// <summary>
    /// The <see cref="IOrderDbContext"/> resolved by the nested <c>Send</c> issued from inside each
    /// nesting subscriber's own <c>Handle</c>, keyed by that subscriber's own marker.
    /// </summary>
    public IReadOnlyDictionary<string, IOrderDbContext> NestedSendInstances => _nestedSendInstances;

    /// <summary>
    /// The <see cref="IOrderDbContext"/> resolved by the <c>Send</c> issued after the publish returned.
    /// </summary>
    public IOrderDbContext? OutsideSendInstance { get; private set; }

    /// <summary>
    /// The <see cref="IOrderDbContext"/> resolved by the <c>Post</c> issued after the publish returned.
    /// </summary>
    public IOrderDbContext? OutsidePostInstance { get; private set; }

    public void RecordRequestScopeInstance(IOrderDbContext context) => RequestScopeInstance = context;

    public void RecordSubscriberInstance(string marker, IOrderDbContext context) => _subscriberInstances[marker] = context;

    public void RecordNestedSendInstance(string marker, IOrderDbContext context) => _nestedSendInstances[marker] = context;

    public void RecordOutsideSendInstance(IOrderDbContext context) => OutsideSendInstance = context;

    public void RecordOutsidePostInstance(IOrderDbContext context) => OutsidePostInstance = context;
}
