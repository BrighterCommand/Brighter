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


using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Inbox.MongoDb;
using Paramore.Brighter.MongoDbTests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MongoDbTests.Inbox;

[Trait("Category", "MongoDb")]
public class MongoDbInboxDuplicateMessageAsyncTests : IDisposable
{
    private readonly string _collection;
    private readonly MongoDbInbox _inbox;
    private readonly MyCommand _raisedCommand;
    private readonly string _contextKey;

    public MongoDbInboxDuplicateMessageAsyncTests()
    {
        _collection = $"inbox-{Guid.NewGuid():N}";
        _inbox = new MongoDbInbox(Configuration.Create(_collection));
        _raisedCommand = new MyCommand { Value = "Test" };
        _contextKey = "test-context";
    }

    [Fact]
    public async Task When_The_Message_Is_Already_In_The_Inbox_Async()
    {
        await _inbox.AddAsync(_raisedCommand, _contextKey);

        var exception = await Catch.ExceptionAsync(() => _inbox.AddAsync(_raisedCommand, _contextKey));

        //_should_succeed_even_if_the_message_is_a_duplicate
        exception.Should().BeNull();
        var exists = await _inbox.ExistsAsync<MyCommand>(_raisedCommand.Id, _contextKey);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task When_The_Message_Is_Already_In_The_Inbox_Different_Context()
    {
        await _inbox.AddAsync(_raisedCommand, "some other key");

        var storedCommand = _inbox.Get<MyCommand>(_raisedCommand.Id, "some other key");

        //_should_read_the_command_from_the__dynamo_db_inbox
        storedCommand.Should().NotBeNull();
    }

    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
