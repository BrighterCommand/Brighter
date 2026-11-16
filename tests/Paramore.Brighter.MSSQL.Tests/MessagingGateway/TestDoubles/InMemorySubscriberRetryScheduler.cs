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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway.TestDoubles;

public sealed class InMemorySubscriberRetryScheduler : IAmAMessageRequeueSchedulerSync, IAmAMessageRequeueSchedulerAsync
{
    public Message? Message { get; private set; }
    public ChannelName? Destination { get; private set; }
    public TimeSpan Delay { get; private set; }
    public CancellationToken Cancellation { get; private set; }
    public bool FailScheduling { get; set; }
    public int AsyncCalls { get; private set; }

    public void Requeue(Message message, ChannelName destination, TimeSpan delay)
    {
        if (FailScheduling)
            throw new InvalidOperationException("Scheduling failed");
        Message = message;
        Destination = destination;
        Delay = delay;
    }

    public Task RequeueAsync(Message message, ChannelName destination, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        AsyncCalls++;
        cancellationToken.ThrowIfCancellationRequested();
        Cancellation = cancellationToken;
        Requeue(message, destination, delay);
        return Task.CompletedTask;
    }
}
