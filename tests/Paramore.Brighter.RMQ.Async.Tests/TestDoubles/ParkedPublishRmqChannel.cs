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

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

// Forward everything to the real channel, but park BasicPublishAsync until the test releases it.
// A channel wrapped with failWhenReleased throws AlreadyClosedException on release instead of publishing,
// as a tracked publish does when its channel shuts down under it.
public class ParkedPublishRmqChannel : DispatchProxy
{
    private IChannel _channel = null!;
    private bool _failWhenReleased;
    private volatile bool _closed;
    private readonly TaskCompletionSource _publishParked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task PublishParked => _publishParked.Task;

    public static ParkedPublishRmqChannel Wrap(IChannel channel, bool failWhenReleased)
    {
        var proxy = Create<IChannel, ParkedPublishRmqChannel>();
        var parked = (ParkedPublishRmqChannel)proxy;
        parked._channel = channel;
        parked._failWhenReleased = failWhenReleased;
        return parked;
    }

    // Report the channel as closed, as it is once the broker or a shared connection has dropped it
    public void MarkClosed() => _closed = true;

    public void Release() => _released.TrySetResult();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;

        switch (method.Name)
        {
            case "get_IsClosed":
                return _closed || _channel.IsClosed;
            case "get_IsOpen":
                return !_closed && _channel.IsOpen;
            case "BasicPublishAsync":
                return ParkThenPublish(method, args);
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

    private async ValueTask ParkThenPublish(MethodInfo method, object?[]? args)
    {
        _publishParked.TrySetResult();
        await _released.Task;

        if (_failWhenReleased)
            throw new AlreadyClosedException(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "channel closed"));

        try
        {
            await (ValueTask)method.Invoke(_channel, args)!;
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        }
    }
}
