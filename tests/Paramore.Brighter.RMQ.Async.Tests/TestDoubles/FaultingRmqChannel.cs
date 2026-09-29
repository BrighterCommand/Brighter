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
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

// Forward broker I/O to the real channel, except for the selected cleanup failure.
public class FaultingRmqChannel : DispatchProxy
{
    private IChannel _channel = null!;
    private Func<string?> _failingOperation = null!;

    public static IChannel Wrap(IChannel channel, Func<string?> failingOperation)
    {
        var proxy = Create<IChannel, FaultingRmqChannel>();
        var fault = (FaultingRmqChannel)proxy;
        fault._channel = channel;
        fault._failingOperation = failingOperation;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        if (method.Name == _failingOperation())
        {
            var error = new InvalidOperationException("Injected channel cleanup failure.");
            if (method.ReturnType == typeof(Task)) return Task.FromException(error);
            if (method.ReturnType == typeof(ValueTask)) return new ValueTask(Task.FromException(error));
            throw error;
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
