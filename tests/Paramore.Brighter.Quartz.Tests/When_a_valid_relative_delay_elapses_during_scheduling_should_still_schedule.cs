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

using System.Collections.Specialized;
using Quartz;
using Paramore.Brighter;
using Paramore.Brighter.Scheduler;
using Paramore.Brighter.MessageScheduler.Quartz;
using ParamoreBrighter.Quartz.Tests.TestDoubles;
using Assert = Xunit.Assert;

namespace ParamoreBrighter.Quartz.Tests;

[Xunit.Collection("Scheduler")]
public class QuartzSchedulerDeadlineTests : Xunit.IAsyncLifetime
{
    private readonly AdvancingDeadlineTimeProvider _clock = new();
    private readonly Message _message = new(
        new MessageHeader(Id.Random(), new RoutingKey("arn:aws:sns:us-east-1:123456789012:deadline"), MessageType.MT_DOCUMENT),
        new MessageBody("deadline payload"));
    private readonly MyEvent _request = new MyEvent();
    private readonly RequestContext _context = new();
    private IScheduler _backend = null!;
    private QuartzScheduler _scheduler = null!;
    private const string GROUP = "deadline";

    public async Task InitializeAsync()
    {
        _backend = await SchedulerBuilder.Create(new NameValueCollection
            { ["quartz.scheduler.instanceName"] = $"deadline-{Guid.NewGuid():N}" })
            .Build().GetScheduler();
        _scheduler = new QuartzScheduler(_backend, GROUP, _clock, _ => "deadline-id", _ => "deadline-id");
        _context.Bag[RequestContextBagNames.PartitionKey] = new PartitionKey("deadline-partition");
    }

    public Task DisposeAsync() => _backend.Shutdown();

    [Xunit.Theory]
    [Xunit.InlineData("message", false, 0)]
    [Xunit.InlineData("message", false, 1)]
    [Xunit.InlineData("message", true, 0)]
    [Xunit.InlineData("message", true, 1)]
    [Xunit.InlineData("request", false, 0)]
    [Xunit.InlineData("request", false, 1)]
    [Xunit.InlineData("request", true, 0)]
    [Xunit.InlineData("request", true, 1)]
    [Xunit.InlineData("request-context", false, 0)]
    [Xunit.InlineData("request-context", false, 1)]
    [Xunit.InlineData("request-context", true, 0)]
    [Xunit.InlineData("request-context", true, 1)]
    [Xunit.InlineData("reschedule", false, 0)]
    [Xunit.InlineData("reschedule", false, 1)]
    [Xunit.InlineData("reschedule", true, 0)]
    [Xunit.InlineData("reschedule", true, 1)]
    public async Task When_a_valid_relative_delay_elapses_during_scheduling_should_still_schedule(
        string operation, bool isAsync, int delaySeconds)
    {
        //Arrange
        await PrepareAsync(operation);
        var delay = TimeSpan.FromSeconds(delaySeconds);

        //Act
        var result = await InvokeAsync(operation, isAsync, delay);

        //Assert
        Assert.Equal(operation == "reschedule" ? bool.TrueString : "deadline-id", result);
        var trigger = await _backend.GetTrigger(new TriggerKey("deadline-id-trigger", GROUP));
        Assert.NotNull(trigger);
        Assert.Equal(_clock.InitialUtcNow.Add(delay), trigger.StartTimeUtc);
        if (operation == "request-context")
        {
            var job = await _backend.GetJobDetail(new JobKey("deadline-id", GROUP));
            var envelope = System.Text.Json.JsonSerializer.Deserialize<Paramore.Brighter.Scheduler.Events.FireSchedulerRequest>(
                job!.JobDataMap.GetString("request")!, Paramore.Brighter.JsonConverters.JsonSerialisationOptions.Options)!;
            Assert.Contains("deadline-partition", envelope.RequestContextData!);
        }
    }

    [Xunit.Theory]
    [Xunit.InlineData("message", false, "negative-delay")]
    [Xunit.InlineData("message", false, "past-date")]
    [Xunit.InlineData("message", true, "negative-delay")]
    [Xunit.InlineData("message", true, "past-date")]
    [Xunit.InlineData("request", false, "negative-delay")]
    [Xunit.InlineData("request", false, "past-date")]
    [Xunit.InlineData("request", true, "negative-delay")]
    [Xunit.InlineData("request", true, "past-date")]
    [Xunit.InlineData("request-context", false, "negative-delay")]
    [Xunit.InlineData("request-context", false, "past-date")]
    [Xunit.InlineData("request-context", true, "negative-delay")]
    [Xunit.InlineData("request-context", true, "past-date")]
    [Xunit.InlineData("reschedule", false, "negative-delay")]
    [Xunit.InlineData("reschedule", false, "past-date")]
    [Xunit.InlineData("reschedule", true, "negative-delay")]
    [Xunit.InlineData("reschedule", true, "past-date")]
    public async Task When_a_schedule_has_invalid_timing_should_reject_it_before_calling_the_backend(
        string operation, bool isAsync, string invalidTiming)
    {
        //Arrange
        await PrepareAsync(operation);
        TimeSpan? delay = invalidTiming == "negative-delay" ? TimeSpan.FromSeconds(-1) : null;

        //Act
        var exception = await Xunit.Record.ExceptionAsync(() => InvokeAsync(operation, isAsync, delay));

        //Assert
        var invalid = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal(delay.HasValue ? "delay" : "at", invalid.ParamName);
        var trigger = await _backend.GetTrigger(new TriggerKey("deadline-id-trigger", GROUP));
        if (operation == "reschedule")
            Assert.Equal(_clock.InitialUtcNow.AddHours(1), trigger!.StartTimeUtc);
        else
            Assert.Null(trigger);
    }

    private async Task PrepareAsync(string operation)
    {
        if (operation == "reschedule")
            await _scheduler.ScheduleAsync(_message, _clock.InitialUtcNow.AddHours(1));
        _clock.Reset();
    }

    private async Task<string> InvokeAsync(string operation, bool isAsync, TimeSpan? delay)
    {
        var at = _clock.InitialUtcNow.AddSeconds(-1);
        if (isAsync)
        {
            return operation switch
            {
                "message" => delay.HasValue
                    ? await _scheduler.ScheduleAsync(_message, delay.Value)
                    : await _scheduler.ScheduleAsync(_message, at),
                "request" => delay.HasValue
                    ? await _scheduler.ScheduleAsync(_request, RequestSchedulerType.Post, delay.Value)
                    : await _scheduler.ScheduleAsync(_request, RequestSchedulerType.Post, at),
                "request-context" => delay.HasValue
                    ? await _scheduler.ScheduleAsync(_request, RequestSchedulerType.Post, delay.Value, _context, CancellationToken.None)
                    : await _scheduler.ScheduleAsync(_request, RequestSchedulerType.Post, at, _context, CancellationToken.None),
                "reschedule" => delay.HasValue
                    ? (await _scheduler.ReSchedulerAsync("deadline-id", delay.Value)).ToString()
                    : (await _scheduler.ReSchedulerAsync("deadline-id", at)).ToString(),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
        }

        return operation switch
        {
            "message" => delay.HasValue
                ? _scheduler.Schedule(_message, delay.Value)
                : _scheduler.Schedule(_message, at),
            "request" => delay.HasValue
                ? _scheduler.Schedule(_request, RequestSchedulerType.Post, delay.Value)
                : _scheduler.Schedule(_request, RequestSchedulerType.Post, at),
            "request-context" => delay.HasValue
                ? _scheduler.Schedule(_request, RequestSchedulerType.Post, delay.Value, _context)
                : _scheduler.Schedule(_request, RequestSchedulerType.Post, at, _context),
            "reschedule" => delay.HasValue
                ? (_scheduler.ReScheduler("deadline-id", delay.Value)).ToString()
                : (_scheduler.ReScheduler("deadline-id", at)).ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }
}
