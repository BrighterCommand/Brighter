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
using Paramore.Brighter;
using Paramore.Brighter.Testing;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorObserveTests
{
    private readonly SpyCommandProcessor _spy;
    private readonly TestCommand _command1;
    private readonly TestCommand _command2;

    public SpyCommandProcessorObserveTests()
    {
        //Arrange
        _spy = new SpyCommandProcessor();
        _command1 = new TestCommand("first");
        _command2 = new TestCommand("second");

        //Act
        _spy.Send(_command1);
        _spy.Send(_command2);
    }

    [Fact]
    public void Then_first_observe_returns_first_request()
    {
        //Act
        var observed = _spy.Observe<TestCommand>();

        //Assert
        observed.ShouldBeSameAs(_command1);
        observed.Name.ShouldBe("first");
    }

    [Fact]
    public void Then_second_observe_returns_second_request()
    {
        //Act
        _spy.Observe<TestCommand>(); // Dequeue first
        var observed = _spy.Observe<TestCommand>();

        //Assert
        observed.ShouldBeSameAs(_command2);
        observed.Name.ShouldBe("second");
    }

    [Fact]
    public void Then_observe_throws_when_queue_is_empty()
    {
        //Act
        _spy.Observe<TestCommand>(); // Dequeue first
        _spy.Observe<TestCommand>(); // Dequeue second

        //Assert
        Should.Throw<InvalidOperationException>(() => _spy.Observe<TestCommand>());
    }

    [Fact]
    public void Then_observe_filters_by_request_type()
    {
        //Arrange - add a different type
        _spy.Publish(new TestEvent());

        //Act
        var observed = _spy.Observe<TestEvent>();

        //Assert
        observed.ShouldBeOfType<TestEvent>();
    }

    private sealed class TestCommand(string name) : Command(Id.Random())
    {
        public string Name { get; } = name;
    }

    private sealed class TestEvent() : Event(Id.Random());
}
