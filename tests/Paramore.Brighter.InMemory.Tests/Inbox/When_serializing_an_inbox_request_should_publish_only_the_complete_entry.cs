#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia

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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Inbox;

[Trait("Category", "InMemory")]
public class InMemoryInboxEntryPublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_serializing_an_inbox_request_should_publish_only_the_complete_entry(bool useAsync)
    {
        //Arrange
        var inbox = new InMemoryInbox(new FakeTimeProvider()) { EntryLimit = -1 };
        const string contextKey = "publication-handler";
        var context = new RequestContext();
        context.Bag[RequestContextBagNames.CausationId] = "complete-causation";
        var command = new InboxSerializationCommand { Value = "complete payload" };
        bool? existedDuringSerialization = null;
        int? countDuringSerialization = null;
        command.OnSerializing = () =>
        {
            existedDuringSerialization = inbox.Exists<InboxSerializationCommand>(command.Id, contextKey, context);
            countDuringSerialization = inbox.EntryCount;
        };

        //Act
        if (useAsync)
            await inbox.AddAsync(command, contextKey, context);
        else
            inbox.Add(command, contextKey, context);

        //Assert
        Assert.False(existedDuringSerialization);
        Assert.Equal(0, countDuringSerialization);
        Assert.True(inbox.Exists<InboxSerializationCommand>(command.Id, contextKey, context));
        Assert.Equal(1, inbox.EntryCount);
        var stored = inbox.Get<InboxSerializationCommand>(command.Id, contextKey, context);
        Assert.Equal(command.Id, stored.Id);
        Assert.Equal("complete payload", stored.Value);
        Assert.Equal("complete-causation", inbox.GetCausationId(command.Id, contextKey, context));
    }
}
