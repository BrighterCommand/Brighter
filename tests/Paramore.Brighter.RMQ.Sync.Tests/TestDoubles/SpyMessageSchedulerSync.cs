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
using System.Collections.Generic;

namespace Paramore.Brighter.RMQ.Sync.Tests.TestDoubles;

public class SpyMessageSchedulerSync : IAmAMessageSchedulerSync
{
    private readonly List<Message> _scheduled = [];

    public IReadOnlyList<Message> Scheduled => _scheduled;

    public string Schedule(Message message, DateTimeOffset at) => Record(message);

    public string Schedule(Message message, TimeSpan delay) => Record(message);

    public bool ReScheduler(string schedulerId, DateTimeOffset at) => true;

    public bool ReScheduler(string schedulerId, TimeSpan delay) => true;

    public void Cancel(string id)
    {
    }

    private string Record(Message message)
    {
        _scheduled.Add(message);
        return Guid.NewGuid().ToString();
    }
}
