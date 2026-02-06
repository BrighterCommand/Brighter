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

using Paramore.Brighter;
using Paramore.Brighter.Testing;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorCountTests
{
    private readonly SpyCommandProcessor _spy = new();

    [Fact]
    public void Then_call_count_returns_zero_before_any_calls()
    {
        //Assert
        _spy.CallCount(CommandType.Send).ShouldBe(0);
    }

    [Fact]
    public void Then_call_count_returns_one_after_single_call()
    {
        //Act
        _spy.Send(new TestCommand());

        //Assert
        _spy.CallCount(CommandType.Send).ShouldBe(1);
    }

    [Fact]
    public void Then_call_count_returns_three_after_three_calls()
    {
        //Act
        _spy.Send(new TestCommand());
        _spy.Send(new TestCommand());
        _spy.Send(new TestCommand());

        //Assert
        _spy.CallCount(CommandType.Send).ShouldBe(3);
    }

    [Fact]
    public void Then_call_count_returns_zero_for_different_method()
    {
        //Act
        _spy.Send(new TestCommand());
        _spy.Send(new TestCommand());

        //Assert
        _spy.CallCount(CommandType.Publish).ShouldBe(0);
    }

    private sealed class TestCommand() : Command(Id.Random());
}
