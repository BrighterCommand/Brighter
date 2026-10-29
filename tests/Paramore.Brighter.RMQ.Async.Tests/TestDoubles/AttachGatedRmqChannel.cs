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

#nullable enable

using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

// Forward everything to the real channel, count the producer's ack subscribers, and hold each caller
// of add_BasicAcksAsync until a second caller has arrived (or the gate times out), so that two sends are
// inside the producer's attach to this channel at the same moment.
public class AttachGatedRmqChannel : DispatchProxy
{
    private static readonly TimeSpan s_gateTimeout = TimeSpan.FromSeconds(2);

    private IChannel _channel = null!;
    private readonly ManualResetEventSlim _bothAttaching = new(false);
    private int _attachers;
    private int _ackSubscriberCount;

    public int AckSubscriberCount => Volatile.Read(ref _ackSubscriberCount);

    public static AttachGatedRmqChannel Wrap(IChannel channel)
    {
        var proxy = Create<IChannel, AttachGatedRmqChannel>();
        var gated = (AttachGatedRmqChannel)proxy;
        gated._channel = channel;
        return gated;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;

        switch (method.Name)
        {
            case "add_BasicAcksAsync":
                HoldUntilBothAreAttaching();
                Interlocked.Increment(ref _ackSubscriberCount);
                break;
            case "remove_BasicAcksAsync":
                Interlocked.Decrement(ref _ackSubscriberCount);
                break;
        }

        try
        {
            return method.Invoke(_channel, args);
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private void HoldUntilBothAreAttaching()
    {
        if (Interlocked.Increment(ref _attachers) >= 2) _bothAttaching.Set();
        _bothAttaching.Wait(s_gateTimeout);
    }
}
