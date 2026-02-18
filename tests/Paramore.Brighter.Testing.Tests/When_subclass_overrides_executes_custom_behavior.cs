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

public class SpyCommandProcessorSubclassOverrideSendTests
{
    private readonly ThrowingSpyCommandProcessor _spy;
    private readonly TestCommand _command;
    private readonly InvalidOperationException _caughtException;

    public SpyCommandProcessorSubclassOverrideSendTests()
    {
        //Arrange
        _spy = new ThrowingSpyCommandProcessor();
        _command = new TestCommand();

        //Act
        _caughtException = Should.Throw<InvalidOperationException>(() => _spy.Send(_command));
    }

    [Fact]
    public void Then_should_throw_custom_exception()
    {
        //Assert
        _caughtException.Message.ShouldBe("Send is not allowed in this test");
    }

    [Fact]
    public void Then_base_should_have_recorded_the_call()
    {
        //Assert
        _spy.WasCalled(CommandType.Send).ShouldBeTrue();
    }

    [Fact]
    public void Then_request_should_be_captured_in_recorded_calls()
    {
        //Assert
        _spy.RecordedCalls.Count.ShouldBe(1);
        _spy.RecordedCalls[0].Request.ShouldBeSameAs(_command);
    }

    private sealed class TestCommand() : Command(Id.Random());
}

public class SpyCommandProcessorSubclassOverridePublishTests
{
    private readonly ThrowingSpyCommandProcessor _spy;
    private readonly TestEvent _event;
    private readonly InvalidOperationException _caughtException;

    public SpyCommandProcessorSubclassOverridePublishTests()
    {
        //Arrange
        _spy = new ThrowingSpyCommandProcessor();
        _event = new TestEvent();

        //Act
        _caughtException = Should.Throw<InvalidOperationException>(() => _spy.Publish(_event));
    }

    [Fact]
    public void Then_should_throw_custom_exception()
    {
        //Assert
        _caughtException.Message.ShouldBe("Publish is not allowed in this test");
    }

    [Fact]
    public void Then_base_should_have_recorded_the_call()
    {
        //Assert
        _spy.WasCalled(CommandType.Publish).ShouldBeTrue();
    }

    private sealed class TestEvent() : Event(Id.Random());
}

/// <summary>
/// A test subclass that throws on Send and Publish to verify virtual method extensibility.
/// Demonstrates that SpyCommandProcessor methods are virtual and can be overridden.
/// </summary>
public class ThrowingSpyCommandProcessor : SpyCommandProcessor
{
    public override void Send<TRequest>(TRequest command, RequestContext? requestContext = null)
    {
        base.Send(command, requestContext);
        throw new InvalidOperationException("Send is not allowed in this test");
    }

    public override void Publish<TRequest>(TRequest @event, RequestContext? requestContext = null)
    {
        base.Publish(@event, requestContext);
        throw new InvalidOperationException("Publish is not allowed in this test");
    }
}
