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
using Paramore.Brighter.Testing;
using Shouldly;
using Xunit;

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorDepositTests
{
    private readonly SpyCommandProcessor _spy = new();
    private readonly MyCommand _command1 = new() { Value = "first" };
    private readonly MyCommand _command2 = new() { Value = "second" };

    [Fact]
    public void Should_return_id_from_deposit_post()
    {
        // Arrange - command has a known Id

        // Act
        var returnedId = _spy.DepositPost(_command1);

        // Assert
        returnedId.ShouldBe(_command1.Id);
    }

    [Fact]
    public void Should_track_deposited_request_by_id()
    {
        // Arrange
        var id = _spy.DepositPost(_command1);

        // Act
        var depositedRequest = _spy.DepositedRequests[id];

        // Assert
        depositedRequest.ShouldBe(_command1);
    }

    [Fact]
    public void Should_track_multiple_deposits_independently()
    {
        // Arrange & Act
        var id1 = _spy.DepositPost(_command1);
        var id2 = _spy.DepositPost(_command2);

        // Assert
        _spy.DepositedRequests[id1].ShouldBe(_command1);
        _spy.DepositedRequests[id2].ShouldBe(_command2);
        _spy.DepositedRequests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Should_track_async_deposits()
    {
        // Arrange & Act
        var id = await _spy.DepositPostAsync(_command1);

        // Assert
        _spy.DepositedRequests[id].ShouldBe(_command1);
    }

    [Fact]
    public void Should_not_add_deposited_requests_to_observation_queue()
    {
        // Arrange & Act
        _spy.DepositPost(_command1);

        // Assert - DepositPost should NOT add to observation queue (that's ClearOutbox's job)
        var observeAction = () => _spy.Observe<MyCommand>();
        observeAction.ShouldThrow<System.InvalidOperationException>();
    }

    private sealed class MyCommand : Command
    {
        public string Value { get; set; } = string.Empty;
        public MyCommand() : base(Id.Random()) { }
    }
}
