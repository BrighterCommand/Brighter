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

using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorRecordTests
{
    private readonly SpyCommandProcessor _spy = new();

    [Fact]
    public async Task Then_send_async_should_record_send_async()
    {
        //Act
        await _spy.SendAsync(new TestCommand());

        //Assert
        _spy.Commands.ShouldContain(CommandType.SendAsync);
    }

    [Fact]
    public async Task Then_publish_async_should_record_publish_async()
    {
        //Act
        await _spy.PublishAsync(new TestEvent());

        //Assert
        _spy.Commands.ShouldContain(CommandType.PublishAsync);
    }

    [Fact]
    public async Task Then_post_async_should_record_post_async()
    {
        //Act
        await _spy.PostAsync(new TestCommand());

        //Assert
        _spy.Commands.ShouldContain(CommandType.PostAsync);
    }

    [Fact]
    public async Task Then_deposit_post_async_should_record_deposit_async()
    {
        //Act
        await _spy.DepositPostAsync(new TestCommand());

        //Assert
        _spy.Commands.ShouldContain(CommandType.DepositAsync);
    }

    [Fact]
    public async Task Then_clear_outbox_async_should_record_clear_async()
    {
        //Act
        await _spy.ClearOutboxAsync([Id.Random()]);

        //Assert
        _spy.Commands.ShouldContain(CommandType.ClearAsync);
    }

    private sealed class TestCommand() : Command(Id.Random());

    private sealed class TestEvent() : Event(Id.Random());
}
