#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia

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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Mediator;

namespace Paramore.Brighter.Core.Tests.Workflows.TestDoubles;

internal sealed class InMemoryScheduleFailureStateStore(TimeProvider timeProvider) : IAmAStateStoreAsync
{
    private readonly InMemoryStateStoreAsync _inner = new(timeProvider);

    public Exception? NextSaveException { get; set; }

    public DateTimeOffset? LastSavedDueTime { get; private set; }

    public async Task SaveJobAsync<TData>(Job<TData>? job, CancellationToken cancellationToken = default)
    {
        if (NextSaveException is { } exception)
        {
            NextSaveException = null;
            throw exception;
        }

        await _inner.SaveJobAsync(job, cancellationToken);
        LastSavedDueTime = job?.DueTime;
    }

    public Task<Job?> GetJobAsync(string? id) => _inner.GetJobAsync(id);

    public Task<IEnumerable<Job>> GetDueJobsAsync(TimeSpan jobAge, CancellationToken cancellationToken = default)
        => _inner.GetDueJobsAsync(jobAge, cancellationToken);
}
