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
using Paramore.Brighter.Testing;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorClearTests
{
    private readonly SpyCommandProcessor _spy = new();
    private readonly MyCommand _command = new() { Value = "test" };

    [Fact]
    public void Should_clear_recorded_calls()
    {
        // Arrange
        _spy.Send(_command);
        _spy.RecordedCalls.Count.ShouldBe(1);

        // Act
        _spy.Reset();

        // Assert
        _spy.RecordedCalls.ShouldBeEmpty();
    }

    [Fact]
    public void Should_clear_commands()
    {
        // Arrange
        _spy.Send(_command);
        _spy.Commands.Count.ShouldBe(1);

        // Act
        _spy.Reset();

        // Assert
        _spy.Commands.ShouldBeEmpty();
    }

    [Fact]
    public void Should_clear_observation_queue()
    {
        // Arrange
        _spy.Send(_command);
        _spy.Observe<MyCommand>().ShouldBe(_command); // Confirm it was observable

        _spy.Send(_command); // Add another one

        // Act
        _spy.Reset();

        // Assert - observation queue is empty
        var observeAction = () => _spy.Observe<MyCommand>();
        observeAction.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Should_clear_deposited_requests()
    {
        // Arrange
        var id = _spy.DepositPost(_command);
        _spy.DepositedRequests.Count.ShouldBe(1);

        // Act
        _spy.Reset();

        // Assert
        _spy.DepositedRequests.ShouldBeEmpty();
    }

    [Fact]
    public void Should_reset_was_called_to_false()
    {
        // Arrange
        _spy.Send(_command);
        _spy.WasCalled(CommandType.Send).ShouldBeTrue();

        // Act
        _spy.Reset();

        // Assert
        _spy.WasCalled(CommandType.Send).ShouldBeFalse();
    }

    [Fact]
    public void Should_reset_call_count_to_zero()
    {
        // Arrange
        _spy.Send(_command);
        _spy.Send(_command);
        _spy.CallCount(CommandType.Send).ShouldBe(2);

        // Act
        _spy.Reset();

        // Assert
        _spy.CallCount(CommandType.Send).ShouldBe(0);
    }

    private sealed class MyCommand : Command
    {
        public string Value { get; set; } = string.Empty;
        public MyCommand() : base(Id.Random()) { }
    }
}
