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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Mediator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Workflows;

public class SchedulerWaitDeadlineHandoffTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_a_runner_waits_before_enqueue_returns_should_preserve_the_new_deadline(
        bool alreadyScheduled, bool useBatch)
    {
        //Arrange
        var clock = new FakeTimeProvider();
        var store = new InMemoryStateStoreAsync(clock);
        var channel = new InMemoryRunnerCoordinatedJobChannel();
        var scheduler = new Scheduler<WorkflowTestData>(channel, store, clock);
        var processor = new CommandProcessor(
            new SubscriberRegistry(),
            new SimpleHandlerFactoryAsync(_ => throw new InvalidOperationException("No handler is needed")),
            new InMemoryRequestContextFactory(),
            new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory());
        var runner = new Runner<WorkflowTestData>(channel, store, processor, scheduler);
        var job = new Job<WorkflowTestData>(new WorkflowTestData());
        var delay = TimeSpan.FromMinutes(5);
        var completion = new Sequential<WorkflowTestData>(
            "Complete",
            new ChangeAsync<WorkflowTestData>(data =>
            {
                data.Bag["completed"] = true;
                return Task.CompletedTask;
            }),
            null,
            null);
        job.InitSteps(new Wait<WorkflowTestData>("Wait", delay, completion));
        if (alreadyScheduled)
        {
            await scheduler.ScheduleAtAsync(job, TimeSpan.FromMinutes(1));
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runnerTask = runner.RunAsync(timeout.Token);
        try
        {
            //Act
            if (useBatch)
                await scheduler.ScheduleAsync(new[] { job }, timeout.Token);
            else
                await scheduler.ScheduleAsync(job, timeout.Token);

            //Assert
            Assert.Equal(clock.GetUtcNow() + delay, job.DueTime);
            Assert.True(job.IsScheduled);
            Assert.Equal(JobState.Waiting, job.State);
            Assert.Same(completion, job.CurrentStep());
            Assert.Equal(job.DueTime, (await store.GetJobAsync(job.Id))!.DueTime);
            Assert.Null(channel.DueTimeAtHandoff);

            //Act
            clock.Advance(delay);
            await scheduler.ScheduleAsync(job, timeout.Token);

            //Assert
            Assert.Equal(JobState.Done, job.State);
            Assert.Equal(true, job.Data.Bag["completed"]);
            Assert.False(job.IsScheduled);
        }
        finally
        {
            channel.Stop();
            await runnerTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
