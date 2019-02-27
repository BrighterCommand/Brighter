#region Licence
/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

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
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.Inbox.Sqlite
{
    [Trait("Category", "Sqlite")]
    [Collection("Sqlite Inbox")]
    public class SqliteInboxDuplicateMessageTests : IDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteInbox _sqlInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private Exception _exception;

        public SqliteInboxDuplicateMessageTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupCommandDb();
            _sqlInbox = new SqliteInbox(new SqliteInboxConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName));
            _raisedCommand = new MyCommand { Value = "Test" };
            _contextKey = "context-key";
            _sqlInbox.Add(_raisedCommand, _contextKey);
        }

        [Fact]
        public void When_The_Message_Is_Already_In_The_Inbox()
        {
            _exception = Catch.Exception(() => _sqlInbox.Add(_raisedCommand, _contextKey));

            //_should_succeed_even_if_the_message_is_a_duplicate
            _exception.Should().BeNull();
            _sqlInbox.Exists<MyCommand>(_raisedCommand.Id, _contextKey).Should().BeTrue();
        }

        public void Dispose()
        {
            _sqliteTestHelper.CleanUpDb();
        }
    }
}
