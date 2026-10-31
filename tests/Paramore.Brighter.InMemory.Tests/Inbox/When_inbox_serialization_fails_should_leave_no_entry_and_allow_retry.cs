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

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.InMemory.Tests.Data;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Inbox;

[Trait("Category", "InMemory")]
public class InMemoryInboxSerializationFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_inbox_serialization_fails_should_leave_no_entry_and_allow_retry(bool useAsync)
    {
        //Arrange
        var inbox = new InMemoryInbox(new FakeTimeProvider()) { EntryLimit = -1 };
        const string contextKey = "serialization-handler";
        var context = new RequestContext();
        context.Bag[RequestContextBagNames.CausationId] = "failed-causation";
        var existing = new SimpleCommand();
        inbox.Add(existing, contextKey, context);
        var command = new InboxSerializationCommand
        {
            Value = "retry payload",
            FailSerialization = true
        };

        async Task AddCommand()
        {
            if (useAsync)
                await inbox.AddAsync(command, contextKey, context);
            else
                inbox.Add(command, contextKey, context);
        }

        //Act
        var error = await Record.ExceptionAsync(AddCommand);

        //Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.False(inbox.Exists<InboxSerializationCommand>(command.Id, contextKey, context));
        Assert.Equal(1, inbox.EntryCount);
        Assert.Null(inbox.GetCausationId(command.Id, contextKey, context));
        Assert.Throws<RequestNotFoundException<InboxSerializationCommand>>(
            () => inbox.Get<InboxSerializationCommand>(command.Id, contextKey, context));
        Assert.Equal(existing.Id, inbox.Get<SimpleCommand>(existing.Id, contextKey, context).Id);
        Assert.Equal("failed-causation", inbox.GetCausationId(existing.Id, contextKey, context));

        //Act
        command.FailSerialization = false;
        context.Bag[RequestContextBagNames.CausationId] = "retry-causation";
        await AddCommand();

        //Assert
        Assert.True(inbox.Exists<InboxSerializationCommand>(command.Id, contextKey, context));
        Assert.Equal(2, inbox.EntryCount);
        var stored = inbox.Get<InboxSerializationCommand>(command.Id, contextKey, context);
        Assert.Equal(command.Id, stored.Id);
        Assert.Equal("retry payload", stored.Value);
        Assert.Equal("retry-causation", inbox.GetCausationId(command.Id, contextKey, context));

        //Act
        command.Value = "rejected replacement";
        command.FailSerialization = true;
        context.Bag[RequestContextBagNames.CausationId] = "rejected-causation";
        var replacementError = await Record.ExceptionAsync(AddCommand);

        //Assert
        Assert.IsType<InvalidOperationException>(replacementError);
        Assert.Equal(2, inbox.EntryCount);
        Assert.Equal("retry payload", inbox.Get<InboxSerializationCommand>(command.Id, contextKey, context).Value);
        Assert.Equal("retry-causation", inbox.GetCausationId(command.Id, contextKey, context));
    }
}
