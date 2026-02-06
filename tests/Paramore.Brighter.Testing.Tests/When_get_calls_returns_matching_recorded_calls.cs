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
using System.Linq;
using Paramore.Brighter.Testing;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorFindRecordedCallsTests
{
    private readonly SpyCommandProcessor _spy = new();
    private readonly MyCommand _command1 = new() { Value = "first" };
    private readonly MyCommand _command2 = new() { Value = "second" };
    private readonly MyEvent _event1 = new() { Data = "event" };

    [Fact]
    public void Should_return_all_calls_for_command_type()
    {
        // Arrange
        _spy.Send(_command1);
        _spy.Publish(_event1);
        _spy.Send(_command2);

        // Act
        var sendCalls = _spy.GetCalls(CommandType.Send);

        // Assert
        sendCalls.ShouldNotBeNull();
        sendCalls.Count().ShouldBe(2);
        sendCalls.Select(c => c.Request).ShouldContain(_command1);
        sendCalls.Select(c => c.Request).ShouldContain(_command2);
    }

    [Fact]
    public void Should_return_recorded_call_with_full_details()
    {
        // Arrange
        var context = new RequestContext();
        _spy.Send(_command1, context);

        // Act
        var calls = _spy.GetCalls(CommandType.Send);

        // Assert
        var call = calls.Single();
        call.Type.ShouldBe(CommandType.Send);
        call.Request.ShouldBe(_command1);
        call.Context.ShouldBe(context);
        call.Timestamp.ShouldBeInRange(
            DateTime.UtcNow.AddSeconds(-5),
            DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Should_return_empty_when_no_matching_calls()
    {
        // Arrange
        _spy.Send(_command1);

        // Act
        var publishCalls = _spy.GetCalls(CommandType.Publish);

        // Assert
        publishCalls.ShouldBeEmpty();
    }

    [Fact]
    public void Should_filter_to_specific_command_type_only()
    {
        // Arrange
        _spy.Send(_command1);
        _spy.Publish(_event1);
        _spy.Post(_command2);

        // Act
        var publishCalls = _spy.GetCalls(CommandType.Publish);

        // Assert
        publishCalls.Count().ShouldBe(1);
        publishCalls.Single().Request.ShouldBe(_event1);
    }

    private sealed class MyCommand : Command
    {
        public string Value { get; set; } = string.Empty;
        public MyCommand() : base(Id.Random()) { }
    }

    private sealed class MyEvent : Event
    {
        public string Data { get; set; } = string.Empty;
        public MyEvent() : base(Id.Random()) { }
    }
}
