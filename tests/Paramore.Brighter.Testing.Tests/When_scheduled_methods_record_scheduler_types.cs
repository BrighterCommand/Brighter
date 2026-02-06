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

using System;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.Testing;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorScheduledSendTests
{
    private readonly SpyCommandProcessor _spy;
    private readonly string _sendAtId;
    private readonly string _sendDelayId;

    public SpyCommandProcessorScheduledSendTests()
    {
        //Arrange
        _spy = new SpyCommandProcessor();
        var command = new TestCommand();
        var at = DateTimeOffset.UtcNow.AddMinutes(5);
        var delay = TimeSpan.FromMinutes(10);

        //Act
        _sendAtId = _spy.Send(at, command);
        _sendDelayId = _spy.Send(delay, command);
    }

    [Fact]
    public void Then_should_record_scheduler_type_for_send_at()
    {
        //Assert
        _spy.Commands[0].ShouldBe(CommandType.Scheduler);
    }

    [Fact]
    public void Then_should_record_scheduler_type_for_send_delay()
    {
        //Assert
        _spy.Commands[1].ShouldBe(CommandType.Scheduler);
    }

    [Fact]
    public void Then_send_at_should_return_scheduler_id()
    {
        //Assert
        _sendAtId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Then_send_delay_should_return_scheduler_id()
    {
        //Assert
        _sendDelayId.ShouldNotBeNullOrEmpty();
    }

    private sealed class TestCommand() : Command(Id.Random());
}

public class SpyCommandProcessorScheduledPublishTests
{
    private readonly SpyCommandProcessor _spy;
    private readonly string _publishAtId;
    private readonly string _publishDelayId;

    public SpyCommandProcessorScheduledPublishTests()
    {
        //Arrange
        _spy = new SpyCommandProcessor();
        var @event = new TestEvent();
        var at = DateTimeOffset.UtcNow.AddMinutes(5);
        var delay = TimeSpan.FromMinutes(10);

        //Act
        _publishAtId = _spy.Publish(at, @event);
        _publishDelayId = _spy.Publish(delay, @event);
    }

    [Fact]
    public void Then_should_record_scheduler_type_for_publish_at()
    {
        //Assert
        _spy.Commands[0].ShouldBe(CommandType.Scheduler);
    }

    [Fact]
    public void Then_should_record_scheduler_type_for_publish_delay()
    {
        //Assert
        _spy.Commands[1].ShouldBe(CommandType.Scheduler);
    }

    [Fact]
    public void Then_publish_at_should_return_scheduler_id()
    {
        //Assert
        _publishAtId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Then_publish_delay_should_return_scheduler_id()
    {
        //Assert
        _publishDelayId.ShouldNotBeNullOrEmpty();
    }

    private sealed class TestEvent() : Event(Id.Random());
}

public class SpyCommandProcessorScheduledPostTests
{
    private readonly SpyCommandProcessor _spy;
    private readonly string _postAtId;
    private readonly string _postDelayId;

    public SpyCommandProcessorScheduledPostTests()
    {
        //Arrange
        _spy = new SpyCommandProcessor();
        var command = new TestCommand();
        var at = DateTimeOffset.UtcNow.AddMinutes(5);
        var delay = TimeSpan.FromMinutes(10);

        //Act
        _postAtId = _spy.Post(at, command);
        _postDelayId = _spy.Post(delay, command);
    }

    [Fact]
    public void Then_should_record_scheduler_type_for_post_at()
    {
        //Assert
        _spy.Commands[0].ShouldBe(CommandType.Scheduler);
    }

    [Fact]
    public void Then_should_record_scheduler_type_for_post_delay()
    {
        //Assert
        _spy.Commands[1].ShouldBe(CommandType.Scheduler);
    }

    [Fact]
    public void Then_post_at_should_return_scheduler_id()
    {
        //Assert
        _postAtId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Then_post_delay_should_return_scheduler_id()
    {
        //Assert
        _postDelayId.ShouldNotBeNullOrEmpty();
    }

    private sealed class TestCommand() : Command(Id.Random());
}

public class SpyCommandProcessorScheduledAsyncTests
{
    private readonly SpyCommandProcessor _spy;
    private readonly string _sendAsyncAtId;
    private readonly string _publishAsyncDelayId;
    private readonly string _postAsyncAtId;

    public SpyCommandProcessorScheduledAsyncTests()
    {
        //Arrange
        _spy = new SpyCommandProcessor();
        var command = new TestCommand();
        var @event = new TestEvent();
        var at = DateTimeOffset.UtcNow.AddMinutes(5);
        var delay = TimeSpan.FromMinutes(10);

        //Act
        _sendAsyncAtId = _spy.SendAsync(at, command).GetAwaiter().GetResult();
        _publishAsyncDelayId = _spy.PublishAsync(delay, @event).GetAwaiter().GetResult();
        _postAsyncAtId = _spy.PostAsync(at, command).GetAwaiter().GetResult();
    }

    [Fact]
    public void Then_should_record_scheduler_async_for_send()
    {
        //Assert
        _spy.Commands[0].ShouldBe(CommandType.SchedulerAsync);
    }

    [Fact]
    public void Then_should_record_scheduler_async_for_publish()
    {
        //Assert
        _spy.Commands[1].ShouldBe(CommandType.SchedulerAsync);
    }

    [Fact]
    public void Then_should_record_scheduler_async_for_post()
    {
        //Assert
        _spy.Commands[2].ShouldBe(CommandType.SchedulerAsync);
    }

    [Fact]
    public void Then_async_scheduled_methods_should_return_ids()
    {
        //Assert
        _sendAsyncAtId.ShouldNotBeNullOrEmpty();
        _publishAsyncDelayId.ShouldNotBeNullOrEmpty();
        _postAsyncAtId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Then_should_record_requests_in_recorded_calls()
    {
        //Assert
        _spy.RecordedCalls.Count.ShouldBe(3);
        _spy.RecordedCalls[0].Request.ShouldBeOfType<TestCommand>();
        _spy.RecordedCalls[1].Request.ShouldBeOfType<TestEvent>();
        _spy.RecordedCalls[2].Request.ShouldBeOfType<TestCommand>();
    }

    private sealed class TestCommand() : Command(Id.Random());
    private sealed class TestEvent() : Event(Id.Random());
}
