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

using System.Linq;
using Paramore.Brighter.Testing;

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorGetRequestsTests
{
    private readonly SpyCommandProcessor _spy = new();
    private readonly MyCommand _command1 = new() { Value = "first" };
    private readonly MyCommand _command2 = new() { Value = "second" };
    private readonly MyEvent _event1 = new() { Data = "event" };

    [Test]
    public async Task Should_return_all_requests_of_type()
    {
        // Arrange
        _spy.Send(_command1);
        _spy.Publish(_event1);
        _spy.Send(_command2);

        // Act
        var commands = _spy.GetRequests<MyCommand>();

        // Assert
        await Assert.That(commands).IsNotNull();
        await Assert.That(commands.Count()).IsEqualTo(2);
        await Assert.That(commands).Contains(_command1);
        await Assert.That(commands).Contains(_command2);
    }

    [Test]
    public async Task Should_be_non_destructive()
    {
        // Arrange
        _spy.Send(_command1);
        _spy.Send(_command2);

        // Act - call GetRequests multiple times
        var firstCall = _spy.GetRequests<MyCommand>().ToList();
        var secondCall = _spy.GetRequests<MyCommand>().ToList();

        // Assert - both calls return the same results
        await Assert.That(firstCall.Count).IsEqualTo(2);
        await Assert.That(secondCall.Count).IsEqualTo(2);
        await Assert.That(firstCall).IsEquivalentTo(secondCall);
    }

    [Test]
    public async Task Should_return_empty_when_no_matching_requests()
    {
        // Arrange - send only events, no commands
        _spy.Publish(_event1);

        // Act
        var commands = _spy.GetRequests<MyCommand>();

        // Assert
        await Assert.That(commands).IsEmpty();
    }

    [Test]
    public async Task Should_filter_to_specific_type_only()
    {
        // Arrange
        _spy.Send(_command1);
        _spy.Publish(_event1);

        // Act
        var events = _spy.GetRequests<MyEvent>();

        // Assert
        await Assert.That(events.Count()).IsEqualTo(1);
        await Assert.That(events.Single()).IsEqualTo(_event1);
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
