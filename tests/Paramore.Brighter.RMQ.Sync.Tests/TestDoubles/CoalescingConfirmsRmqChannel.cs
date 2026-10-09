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
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;

// Forward broker I/O to the real channel, but hold back the producer's publisher-confirm handlers, so
// the test, not broker timing, decides how acks and nacks are framed.
public class CoalescingConfirmsRmqChannel : DispatchProxy
{
    private IModel _channel = null!;
    private EventHandler<BasicAckEventArgs>? _acks;
    private EventHandler<BasicNackEventArgs>? _nacks;

    public static CoalescingConfirmsRmqChannel Wrap(IModel channel)
    {
        var proxy = Create<IModel, CoalescingConfirmsRmqChannel>();
        var coalescing = (CoalescingConfirmsRmqChannel)proxy;
        coalescing._channel = channel;
        return coalescing;
    }

    // One broker frame: the client raises a single event, carrying the broker's multiple flag as-is
    public void RaiseAck(ulong deliveryTag, bool multiple)
        => _acks?.Invoke(this, new BasicAckEventArgs { DeliveryTag = deliveryTag, Multiple = multiple });

    public void RaiseNack(ulong deliveryTag, bool multiple)
        => _nacks?.Invoke(this, new BasicNackEventArgs { DeliveryTag = deliveryTag, Multiple = multiple });

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        switch (method.Name)
        {
            case "add_BasicAcks":
                _acks += (EventHandler<BasicAckEventArgs>)args![0]!;
                return null;
            case "add_BasicNacks":
                _nacks += (EventHandler<BasicNackEventArgs>)args![0]!;
                return null;
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
}
