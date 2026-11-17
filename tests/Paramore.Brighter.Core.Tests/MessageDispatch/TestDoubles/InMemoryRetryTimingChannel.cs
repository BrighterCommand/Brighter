#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Polly.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

internal sealed class InMemoryRetryTimingChannel : Channel
{
    private readonly bool _brokenCircuit;
    private readonly TaskCompletionSource<TimeSpan[]> _retryDelays;
    private readonly List<TimeSpan> _observedDelays = [];
    private long? _lastFailureTimestamp;

    public InMemoryRetryTimingChannel(Subscription subscription, bool brokenCircuit,
        TaskCompletionSource<TimeSpan[]> retryDelays)
        : base(subscription.ChannelName, subscription.RoutingKey,
            new InMemoryMessageConsumer(subscription.RoutingKey, new InternalBus(), TimeProvider.System))
    {
        _brokenCircuit = brokenCircuit;
        _retryDelays = retryDelays;
        Enqueue(MessageFactory.CreateQuitMessage(subscription.RoutingKey));
    }

    public override Message Receive(TimeSpan? timeOut = null)
    {
        if (_lastFailureTimestamp.HasValue)
            _observedDelays.Add(Stopwatch.GetElapsedTime(_lastFailureTimestamp.Value));

        if (_observedDelays.Count == 2)
        {
            _retryDelays.TrySetResult(_observedDelays.ToArray());
            return base.Receive(timeOut);
        }

        Exception inner = _brokenCircuit
            ? new BrokenCircuitException("The channel circuit is open.")
            : new InvalidOperationException("The channel is unavailable.");
        var failure = new ChannelFailureException("Receive failed.", inner);
        _lastFailureTimestamp = Stopwatch.GetTimestamp();
        throw failure;
    }
}
