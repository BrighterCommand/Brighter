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
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class SchedulerEnqueueFailureDeadlineTests
{
    [Theory]
    [InlineData("closed")]
    [InlineData("cancelled")]
    [InlineData("cancelled-while-full")]
    public async Task When_enqueue_fails_should_preserve_the_existing_schedule(string failure)
    {
        //Arrange
        var clock = new FakeTimeProvider();
        var store = new InMemoryScheduleFailureStateStore(clock);
        var channel = new InMemoryJobChannel<WorkflowTestData>(boundedCapacity: 1);
        var scheduler = new Scheduler<WorkflowTestData>(channel, store, clock);
        var job = new Job<WorkflowTestData>(new WorkflowTestData());
        await scheduler.ScheduleAtAsync(job, TimeSpan.FromMinutes(5));
        var originalDueTime = job.DueTime;
        using var cancellation = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Job<WorkflowTestData>? occupyingJob = null;
        if (failure == "closed")
            channel.Stop();
        else if (failure == "cancelled")
            cancellation.Cancel();
        else
        {
            occupyingJob = new Job<WorkflowTestData>(new WorkflowTestData());
            await channel.EnqueueJobAsync(occupyingJob, timeout.Token);
        }

        //Act
        var scheduling = scheduler.ScheduleAsync(job, cancellation.Token);
        if (failure == "cancelled-while-full")
            cancellation.Cancel();
        var exception = await Record.ExceptionAsync(() => scheduling.WaitAsync(timeout.Token));

        //Assert
        if (failure == "closed")
            Assert.IsType<ChannelClosedException>(exception);
        else
            Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(originalDueTime, job.DueTime);
        Assert.Equal(originalDueTime, store.LastSavedDueTime);
        Assert.True(job.IsScheduled);
        Assert.Equal(originalDueTime, (await store.GetJobAsync(job.Id))!.DueTime);

        if (failure != "closed")
            channel.Stop();
        if (occupyingJob is not null)
            Assert.Same(occupyingJob, await channel.DequeueJobAsync(timeout.Token));
        Assert.Null(await channel.DequeueJobAsync(timeout.Token));
    }
}
