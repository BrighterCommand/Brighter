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

namespace Paramore.Brighter.Testing.Tests;

public class SpyCommandProcessorDepositTests
{
    private readonly SpyCommandProcessor _spy = new();
    private readonly MyCommand _command1 = new() { Value = "first" };
    private readonly MyCommand _command2 = new() { Value = "second" };

    [Test]
    public async Task Should_return_id_from_deposit_post()
    {
        // Arrange - command has a known Id

        // Act
        var returnedId = _spy.DepositPost(_command1);

        // Assert
        await Assert.That(returnedId).IsEqualTo(_command1.Id);
    }

    [Test]
    public async Task Should_track_deposited_request_by_id()
    {
        // Arrange
        var id = _spy.DepositPost(_command1);

        // Act
        var depositedRequest = _spy.DepositedRequests[id];

        // Assert
        await Assert.That(depositedRequest).IsEqualTo(_command1);
    }

    [Test]
    public async Task Should_track_multiple_deposits_independently()
    {
        // Arrange & Act
        var id1 = _spy.DepositPost(_command1);
        var id2 = _spy.DepositPost(_command2);

        // Assert
        await Assert.That(_spy.DepositedRequests[id1]).IsEqualTo(_command1);
        await Assert.That(_spy.DepositedRequests[id2]).IsEqualTo(_command2);
        await Assert.That(_spy.DepositedRequests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Should_track_async_deposits()
    {
        // Arrange & Act
        var id = await _spy.DepositPostAsync(_command1);

        // Assert
        await Assert.That(_spy.DepositedRequests[id]).IsEqualTo(_command1);
    }

    [Test]
    public Task Should_not_add_deposited_requests_to_observation_queue()
    {
        // Arrange & Act
        _spy.DepositPost(_command1);

        // Assert - DepositPost should NOT add to observation queue (that's ClearOutbox's job)
        Assert.ThrowsExactly<InvalidOperationException>(() => _spy.Observe<MyCommand>());
        return Task.CompletedTask;
    }

    private sealed class MyCommand : Command
    {
        public string Value { get; set; } = string.Empty;
        public MyCommand() : base(Id.Random()) { }
    }
}
