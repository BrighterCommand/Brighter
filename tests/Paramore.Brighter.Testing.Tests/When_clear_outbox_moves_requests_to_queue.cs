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
using Paramore.Brighter.Testing;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorOutboxTests
{
    private readonly SpyCommandProcessor _spy = new();
    private readonly MyCommand _command1 = new() { Value = "first" };
    private readonly MyCommand _command2 = new() { Value = "second" };

    [Fact]
    public void Should_not_be_observable_before_clear()
    {
        // Arrange
        _spy.DepositPost(_command1);

        // Act & Assert - request is NOT in observation queue yet
        var observeAction = () => _spy.Observe<MyCommand>();
        observeAction.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Should_be_observable_after_clear()
    {
        // Arrange
        var id = _spy.DepositPost(_command1);

        // Act
        _spy.ClearOutbox([id]);

        // Assert - request IS now in observation queue
        var observed = _spy.Observe<MyCommand>();
        observed.ShouldBe(_command1);
    }

    [Fact]
    public void Should_clear_multiple_requests()
    {
        // Arrange
        var id1 = _spy.DepositPost(_command1);
        var id2 = _spy.DepositPost(_command2);

        // Act
        _spy.ClearOutbox([id1, id2]);

        // Assert - both requests are now observable in order
        _spy.Observe<MyCommand>().ShouldBe(_command1);
        _spy.Observe<MyCommand>().ShouldBe(_command2);
    }

    [Fact]
    public void Should_record_clear_command_type()
    {
        // Arrange
        var id = _spy.DepositPost(_command1);

        // Act
        _spy.ClearOutbox([id]);

        // Assert
        _spy.WasCalled(CommandType.Clear).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_work_with_async_clear()
    {
        // Arrange
        var id = await _spy.DepositPostAsync(_command1);

        // Act
        await _spy.ClearOutboxAsync([id]);

        // Assert
        _spy.Observe<MyCommand>().ShouldBe(_command1);
        _spy.WasCalled(CommandType.ClearAsync).ShouldBeTrue();
    }

    private sealed class MyCommand : Command
    {
        public string Value { get; set; } = string.Empty;
        public MyCommand() : base(Id.Random()) { }
    }
}
