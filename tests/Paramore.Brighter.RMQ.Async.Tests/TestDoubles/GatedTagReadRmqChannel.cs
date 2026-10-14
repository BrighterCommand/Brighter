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

// Forward everything to the real channel, but hold each caller of GetNextPublishSequenceNumberAsync
// after it has read its tag until a second caller has read one too (or the gate times out). Two
// concurrent sends are then both holding a tag before either of them publishes.
public class GatedTagReadRmqChannel : DispatchProxy
{
    private static readonly TimeSpan s_gateTimeout = TimeSpan.FromSeconds(2);

    private IChannel _channel = null!;
    private readonly TaskCompletionSource _bothHaveReadATag = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _tagReads;

    public static GatedTagReadRmqChannel Wrap(IChannel channel)
    {
        var proxy = Create<IChannel, GatedTagReadRmqChannel>();
        var gated = (GatedTagReadRmqChannel)proxy;
        gated._channel = channel;
        return gated;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;

        try
        {
            var result = method.Invoke(_channel, args);
            return method.Name == nameof(IChannel.GetNextPublishSequenceNumberAsync)
                ? HoldUntilBothHaveReadATag((ValueTask<ulong>)result!)
                : result;
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private async ValueTask<ulong> HoldUntilBothHaveReadATag(ValueTask<ulong> read)
    {
        var tag = await read;

        if (Interlocked.Increment(ref _tagReads) >= 2) _bothHaveReadATag.TrySetResult();
        await Task.WhenAny(_bothHaveReadATag.Task, Task.Delay(s_gateTimeout));

        return tag;
    }
}
