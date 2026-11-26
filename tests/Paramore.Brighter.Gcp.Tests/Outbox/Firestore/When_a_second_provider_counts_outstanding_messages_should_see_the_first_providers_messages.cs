#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel <guilhermeaappel@gmail.com>

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

#nullable enable
using System;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Outbox.Firestore;

[Trait("Category", "Firestore")]
[Collection("FirestoreOutbox")]
public class OutstandingCountSharedCollectionTests : IDisposable
{
    private readonly FirestoreOutboxProvider _firstProvider = new();
    private readonly FirestoreOutboxProvider _secondProvider = new();

    public OutstandingCountSharedCollectionTests()
    {
        _firstProvider.DeleteOutstandingCountStore();
        _secondProvider.DeleteOutstandingCountStore();
    }

    [Fact]
    public async Task When_a_second_provider_counts_outstanding_messages_should_see_the_first_providers_messages()
    {
        //Arrange
        var staleMessage = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("outstanding.count"), MessageType.MT_COMMAND, timeStamp: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10)),
            new MessageBody("stale"));
        await _firstProvider.CreateOutstandingCountStore().AddAsync(staleMessage, new RequestContext());

        //Act
        var count = await _secondProvider.CreateOutstandingCountStore()
            .GetOutstandingMessageCountAsync(TimeSpan.FromMinutes(1), new RequestContext(), 100, null);

        //Assert
        Assert.Equal(1, count);
    }

    public void Dispose()
    {
        _firstProvider.DeleteOutstandingCountStore();
        _secondProvider.DeleteOutstandingCountStore();
    }
}
