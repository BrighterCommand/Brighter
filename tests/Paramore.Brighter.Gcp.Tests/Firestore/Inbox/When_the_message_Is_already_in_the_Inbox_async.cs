#region Licence

/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian.cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion


using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.Inbox.Firestore;

namespace Paramore.Brighter.Gcp.Tests.Firestore.Inbox;

[Trait("Category", "Firestore")]
public class InboxDuplicateMessageAsyncTests
{
    private readonly FirestoreInbox _inbox = new(Configuration.CreateInbox());
    private readonly MyCommand _raisedCommand = new() { Value = "Test" };
    private const string ContextKey = "test-context";

    [Fact]
    public async Task When_The_Message_Is_Already_In_The_Inbox_Async()
    {
        await _inbox.AddAsync(_raisedCommand, ContextKey, null);

        var exception = await Catch.ExceptionAsync(() => _inbox.AddAsync(_raisedCommand, ContextKey, null));

        //_should_succeed_even_if_the_message_is_a_duplicate
        Assert.Null(exception);
        var exists = await _inbox.ExistsAsync<MyCommand>(_raisedCommand.Id, ContextKey, null);
        Assert.True(exists);
    }

    [Fact]
    public async Task When_The_Message_Is_Already_In_The_Inbox_Different_Context()
    {
        await _inbox.AddAsync(_raisedCommand, "some other key", null);

        var storedCommand = _inbox.Get<MyCommand>(_raisedCommand.Id, "some other key", null);

        //_should_read_the_command_from_the__dynamo_db_inbox
        Assert.NotNull(storedCommand);
    }
}
