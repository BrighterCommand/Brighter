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

public class SpyCommandProcessorCaptureRequestDetailsTests
{
    private readonly SpyCommandProcessor _spy;
    private readonly TestCommand _command;
    private readonly RequestContext _context;
    private readonly DateTime _beforeCall;
    private readonly DateTime _afterCall;

    public SpyCommandProcessorCaptureRequestDetailsTests()
    {
        //Arrange
        _spy = new SpyCommandProcessor();
        _command = new TestCommand();
        _context = new RequestContext();

        //Act
        _beforeCall = DateTime.UtcNow;
        _spy.Send(_command, _context);
        _afterCall = DateTime.UtcNow;
    }

    [Fact]
    public void Then_recorded_call_should_have_correct_type()
    {
        //Assert
        _spy.RecordedCalls.ShouldHaveSingleItem();
        _spy.RecordedCalls[0].Type.ShouldBe(CommandType.Send);
    }

    [Fact]
    public void Then_recorded_call_should_have_exact_request()
    {
        //Assert
        _spy.RecordedCalls[0].Request.ShouldBeSameAs(_command);
    }

    [Fact]
    public void Then_recorded_call_should_have_timestamp_within_range()
    {
        //Assert
        var timestamp = _spy.RecordedCalls[0].Timestamp;
        timestamp.ShouldBeGreaterThanOrEqualTo(_beforeCall);
        timestamp.ShouldBeLessThanOrEqualTo(_afterCall);
    }

    [Fact]
    public void Then_recorded_call_should_capture_request_context()
    {
        //Assert
        _spy.RecordedCalls[0].Context.ShouldBeSameAs(_context);
    }

    private sealed class TestCommand() : Command(Id.Random());
}
