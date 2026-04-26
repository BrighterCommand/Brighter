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

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorClearTests
{
    private readonly SpyCommandProcessor _spy = new();
    private readonly MyCommand _command = new() { Value = "test" };

    [Test]
    public async Task Should_clear_recorded_calls()
    {
        // Arrange
        _spy.Send(_command);
        await Assert.That(_spy.RecordedCalls.Count).IsEqualTo(1);

        // Act
        _spy.Reset();

        // Assert
        await Assert.That(_spy.RecordedCalls).IsEmpty();
    }

    [Test]
    public async Task Should_clear_commands()
    {
        // Arrange
        _spy.Send(_command);
        await Assert.That(_spy.Commands.Count).IsEqualTo(1);

        // Act
        _spy.Reset();

        // Assert
        await Assert.That(_spy.Commands).IsEmpty();
    }

    [Test]
    public async Task Should_clear_observation_queue()
    {
        // Arrange
        _spy.Send(_command);
        await Assert.That(_spy.Observe<MyCommand>()).IsEqualTo(_command); // Confirm it was observable

        _spy.Send(_command); // Add another one

        // Act
        _spy.Reset();

        // Assert - observation queue is empty
        Assert.ThrowsExactly<InvalidOperationException>(() => _spy.Observe<MyCommand>());
    }

    [Test]
    public async Task Should_clear_deposited_requests()
    {
        // Arrange
        var id = _spy.DepositPost(_command);
        await Assert.That(_spy.DepositedRequests.Count).IsEqualTo(1);

        // Act
        _spy.Reset();

        // Assert
        await Assert.That(_spy.DepositedRequests).IsEmpty();
    }

    [Test]
    public async Task Should_reset_was_called_to_false()
    {
        // Arrange
        _spy.Send(_command);
        await Assert.That(_spy.WasCalled(CommandType.Send)).IsTrue();

        // Act
        _spy.Reset();

        // Assert
        await Assert.That(_spy.WasCalled(CommandType.Send)).IsFalse();
    }

    [Test]
    public async Task Should_reset_call_count_to_zero()
    {
        // Arrange
        _spy.Send(_command);
        _spy.Send(_command);
        await Assert.That(_spy.CallCount(CommandType.Send)).IsEqualTo(2);

        // Act
        _spy.Reset();

        // Assert
        await Assert.That(_spy.CallCount(CommandType.Send)).IsEqualTo(0);
    }

    private sealed class MyCommand : Command
    {
        public string Value { get; set; } = string.Empty;
        public MyCommand() : base(Id.Random()) { }
    }
}
