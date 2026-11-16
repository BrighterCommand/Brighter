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

using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;
using Paramore.Brighter.TickerQ.Tests.TestDoubles.Fixtures;
using TickerQ.Utilities.Interfaces;

namespace Paramore.Brighter.TickerQ.Tests;

[Collection("Scheduler")]
public class TickerQScheduledTimeTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task When_scheduling_within_one_second_should_wait_until_the_scheduled_time(
        bool isRequest, bool isAsync, bool useDateTime)
    {
        //Arrange
        BaseTickerQFixture fixture = isRequest
            ? isAsync ? new TickerQRequestAsyncTestFixture() : new TickerQRequestTestFixture()
            : new TickerQMessageTestFixture();
        var host = fixture.ServiceProvider.GetRequiredService<ITickerQHostScheduler>();
        try
        {
            var request = new MyEvent();
            var message = new Message(
                new MessageHeader { MessageId = request.Id, Topic = fixture.RoutingKey },
                new MessageBody("scheduled message"));
            var publishedAt = new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.MessageProducer.OnMessagePublished += result =>
            {
                if (result.MessageId == request.Id)
                    publishedAt.TrySetResult(fixture.TimeProvider.GetUtcNow());
            };
            var delay = TimeSpan.FromSeconds(1);
            var scheduledFor = fixture.TimeProvider.GetUtcNow().Add(delay);

            //Act
            if (isRequest)
            {
                if (isAsync)
                {
                    var scheduler = fixture.SchedulerFactory.CreateAsync(fixture.Processor);
                    if (useDateTime) await scheduler.ScheduleAsync(request, RequestSchedulerType.Post, scheduledFor);
                    else await scheduler.ScheduleAsync(request, RequestSchedulerType.Post, delay);
                }
                else
                {
                    var scheduler = fixture.SchedulerFactory.CreateSync(fixture.Processor);
                    if (useDateTime) scheduler.Schedule(request, RequestSchedulerType.Post, scheduledFor);
                    else scheduler.Schedule(request, RequestSchedulerType.Post, delay);
                }
            }
            else if (isAsync)
            {
                var scheduler = (IAmAMessageSchedulerAsync)fixture.SchedulerFactory.Create(fixture.Processor);
                if (useDateTime) await scheduler.ScheduleAsync(message, scheduledFor);
                else await scheduler.ScheduleAsync(message, delay);
            }
            else
            {
                var scheduler = (IAmAMessageSchedulerSync)fixture.SchedulerFactory.Create(fixture.Processor);
                if (useDateTime) scheduler.Schedule(message, scheduledFor);
                else scheduler.Schedule(message, delay);
            }

            var actualPublishedAt = await publishedAt.Task.WaitAsync(TimeSpan.FromSeconds(15));

            //Assert
            Assert.True(actualPublishedAt >= scheduledFor,
                $"Message {request.Id} was published at {actualPublishedAt:O}, before its scheduled time {scheduledFor:O}.");
            Assert.Contains(fixture.InternalBus.Stream(fixture.RoutingKey), item => item.Id == request.Id);
        }
        finally
        {
            await host.StopAsync();
            await ((IAsyncDisposable)fixture.ServiceProvider).DisposeAsync();
        }
    }
}
