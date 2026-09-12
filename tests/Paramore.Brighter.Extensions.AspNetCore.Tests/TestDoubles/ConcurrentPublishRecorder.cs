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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records every <see cref="IOrderDbContext"/> resolved during AC-12's concurrent-Publish scenario -
/// the controller's own request-scope instance, each subscriber's own instance, the one a nested
/// <c>SendAsync</c> issued from inside a subscriber resolved, and the ones a <c>Send</c>/<c>Post</c>
/// issued after the publish returned resolved - plus whether each subscriber observed the other
/// arrive at their shared rendezvous. Register as a singleton in the container under test.
/// </summary>
public sealed class ConcurrentPublishRecorder
{
    /// <summary>
    /// The controller's own <see cref="IOrderDbContext"/> - the request-scoped instance <c>R</c>.
    /// </summary>
    public IOrderDbContext? RequestScopeInstance { get; private set; }

    /// <summary>
    /// The first subscriber's own <see cref="IOrderDbContext"/>.
    /// </summary>
    public IOrderDbContext? SubscriberOneInstance { get; private set; }

    /// <summary>
    /// The second subscriber's own <see cref="IOrderDbContext"/>.
    /// </summary>
    public IOrderDbContext? SubscriberTwoInstance { get; private set; }

    /// <summary>
    /// The <see cref="IOrderDbContext"/> resolved by the nested <c>SendAsync</c> the first subscriber
    /// issues from inside its own <c>HandleAsync</c>.
    /// </summary>
    public IOrderDbContext? InnerCommandInstance { get; private set; }

    /// <summary>
    /// The <see cref="IOrderDbContext"/> resolved by the <c>Send</c> issued after the publish returned.
    /// </summary>
    public IOrderDbContext? OutsideSendInstance { get; private set; }

    /// <summary>
    /// The <see cref="IOrderDbContext"/> resolved by the <c>Post</c> issued after the publish returned.
    /// </summary>
    public IOrderDbContext? OutsidePostInstance { get; private set; }

    /// <summary>
    /// Whether the first subscriber's rendezvous wait completed by the second subscriber arriving,
    /// rather than timing out.
    /// </summary>
    public bool SubscriberOneObservedOverlap { get; private set; }

    /// <summary>
    /// Whether the second subscriber's rendezvous wait completed by the first subscriber arriving,
    /// rather than timing out.
    /// </summary>
    public bool SubscriberTwoObservedOverlap { get; private set; }

    public void RecordRequestScopeInstance(IOrderDbContext context) => RequestScopeInstance = context;

    public void RecordSubscriberOneInstance(IOrderDbContext context) => SubscriberOneInstance = context;

    public void RecordSubscriberTwoInstance(IOrderDbContext context) => SubscriberTwoInstance = context;

    public void RecordInnerCommandInstance(IOrderDbContext context) => InnerCommandInstance = context;

    public void RecordOutsideSendInstance(IOrderDbContext context) => OutsideSendInstance = context;

    public void RecordOutsidePostInstance(IOrderDbContext context) => OutsidePostInstance = context;

    public void RecordSubscriberOneObservedOverlap() => SubscriberOneObservedOverlap = true;

    public void RecordSubscriberTwoObservedOverlap() => SubscriberTwoObservedOverlap = true;
}
