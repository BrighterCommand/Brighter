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

public class SpyCommandProcessorCallTests
{
    private readonly SpyCommandProcessor _spy;
    private readonly TestResponse? _response;
    private readonly TestCallRequest _request;

    public SpyCommandProcessorCallTests()
    {
        //Arrange
        _spy = new SpyCommandProcessor();
        _request = new TestCallRequest();

        //Act
        _response = _spy.Call<TestCallRequest, TestResponse>(_request);
    }

    [Fact]
    public void Then_should_record_call_command_type()
    {
        //Assert
        _spy.Commands.ShouldContain(CommandType.Call);
    }

    [Fact]
    public void Then_should_return_null()
    {
        //Assert
        _response.ShouldBeNull();
    }

    [Fact]
    public void Then_should_capture_request_in_recorded_calls()
    {
        //Assert
        _spy.RecordedCalls.Count.ShouldBe(1);
        _spy.RecordedCalls[0].Request.ShouldBeSameAs(_request);
        _spy.RecordedCalls[0].Type.ShouldBe(CommandType.Call);
    }

    [Fact]
    public void Then_was_called_should_return_true()
    {
        //Assert
        _spy.WasCalled(CommandType.Call).ShouldBeTrue();
    }

    [Fact]
    public void Then_call_count_should_be_one()
    {
        //Assert
        _spy.CallCount(CommandType.Call).ShouldBe(1);
    }

    private sealed class TestCallRequest : ICall
    {
        public Id Id { get; set; } = Id.Random();
        public Id? CorrelationId { get; set; }
        public ReplyAddress? ReplyTo { get; set; }
        public ReplyAddress ReplyAddress => new(Id.ToString(), Id.Random().ToString());
    }

    private sealed class TestResponse : IResponse
    {
        public Id Id { get; set; } = Id.Random();
        public Id? CorrelationId { get; set; }
        public ReplyAddress? ReplyTo { get; set; }
        public Id HandledBy { get; set; } = Id.Random();
    }
}
