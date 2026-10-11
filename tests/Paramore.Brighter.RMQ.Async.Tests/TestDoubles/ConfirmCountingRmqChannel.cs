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

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

// Forward everything to the real channel, so the broker still confirms, but count the producer's
// publisher-confirm subscribers, so a test can see which channel the producer is listening to.
public class ConfirmCountingRmqChannel : DispatchProxy
{
    private IChannel _channel = null!;

    public int AckSubscriberCount { get; private set; }
    public int NackSubscriberCount { get; private set; }

    public static ConfirmCountingRmqChannel Wrap(IChannel channel)
    {
        var proxy = Create<IChannel, ConfirmCountingRmqChannel>();
        var counting = (ConfirmCountingRmqChannel)proxy;
        counting._channel = channel;
        return counting;
    }

    // Close the real channel under the producer, as the broker does when it closes a channel
    public Task CloseAsync() => _channel.CloseAsync();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        switch (method.Name)
        {
            case "add_BasicAcksAsync":
                AckSubscriberCount++;
                break;
            case "remove_BasicAcksAsync":
                AckSubscriberCount--;
                break;
            case "add_BasicNacksAsync":
                NackSubscriberCount++;
                break;
            case "remove_BasicNacksAsync":
                NackSubscriberCount--;
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
}
