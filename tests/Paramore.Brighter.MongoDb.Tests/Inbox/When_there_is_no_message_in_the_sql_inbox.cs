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
using FluentAssertions;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.MongoDb;
using Paramore.Brighter.MongoDb.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Inbox;

[Trait("Category", "MongoDb")]
public class MongoDbInboxEmptyWhenSearchedTests : IDisposable
{
    private readonly string _collection;
    private readonly MongoDbInbox _inbox;
    private readonly string _contextKey;

    public MongoDbInboxEmptyWhenSearchedTests()
    {
        _collection = $"inbox-{Guid.NewGuid():N}";
        _inbox = new MongoDbInbox(Configuration.Create(_collection));
        _contextKey = "context-key";
    }

    [Fact]
    public void When_There_Is_No_Message_In_The_Sql_Inbox_And_Call_Get()
    {
        string commandId = Guid.NewGuid().ToString();
        var exception = Catch.Exception(() => _ = _inbox.Get<MyCommand>(commandId, _contextKey));

        exception.Should().BeOfType<RequestNotFoundException<MyCommand>>();
    }

    [Fact]
    public void When_There_Is_No_Message_In_The_Sql_Inbox_And_Call_Exists()
    {
        string commandId = Guid.NewGuid().ToString();
        _inbox.Exists<MyCommand>(commandId, _contextKey).Should().BeFalse();
    }
    
    public void Dispose()
    {
        Configuration.Cleanup(_collection);
    }
}
