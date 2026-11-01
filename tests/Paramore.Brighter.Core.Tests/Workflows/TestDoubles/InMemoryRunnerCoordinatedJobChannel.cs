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

// Completes each enqueue only after the runner has finished that job and requested another.
internal sealed class InMemoryRunnerCoordinatedJobChannel : IAmAJobChannel<WorkflowTestData>
{
    private readonly InMemoryJobChannel<WorkflowTestData> _inner = new();
    private TaskCompletionSource<bool>? _pendingCompletion;
    private TaskCompletionSource<bool>? _lastDequeuedCompletion;

    public DateTimeOffset? DueTimeAtHandoff { get; private set; }

    public async Task EnqueueJobAsync(Job<WorkflowTestData> job, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingCompletion = completion;
        DueTimeAtHandoff = job.DueTime;
        await _inner.EnqueueJobAsync(job, cancellationToken);
        await completion.Task.WaitAsync(cancellationToken);
    }

    public async Task<Job<WorkflowTestData>?> DequeueJobAsync(CancellationToken cancellationToken = default)
    {
        _lastDequeuedCompletion?.TrySetResult(true);
        var job = await _inner.DequeueJobAsync(cancellationToken);
        if (job is not null)
            _lastDequeuedCompletion = _pendingCompletion;
        return job;
    }

    public IAsyncEnumerable<Job<WorkflowTestData>> Stream() => _inner.Stream();

    public bool IsClosed() => _inner.IsClosed();

    public void Stop() => _inner.Stop();
}
