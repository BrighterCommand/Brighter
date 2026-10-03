#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.AWS.Tests.TestDoubles;

internal sealed class InMemoryDelayedMessageScheduler : IAmAMessageSchedulerSync, IAmAMessageSchedulerAsync
{
    public Message? ScheduledMessage { get; private set; }
    public TimeSpan? ScheduledDelay { get; private set; }
    public CancellationToken CancellationToken { get; private set; }
    public bool UsedAsync { get; private set; }

    public string Schedule(Message message, TimeSpan delay)
    {
        ScheduledMessage = message;
        ScheduledDelay = delay;
        return message.Id.Value;
    }

    public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        UsedAsync = true;
        CancellationToken = cancellationToken;
        return Task.FromResult(Schedule(message, delay));
    }

    public string Schedule(Message message, DateTimeOffset at) => throw new NotSupportedException();
    public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public bool ReScheduler(string schedulerId, DateTimeOffset at) => throw new NotSupportedException();
    public bool ReScheduler(string schedulerId, TimeSpan delay) => throw new NotSupportedException();
    public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public void Cancel(string id) => throw new NotSupportedException();
    public Task CancelAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
