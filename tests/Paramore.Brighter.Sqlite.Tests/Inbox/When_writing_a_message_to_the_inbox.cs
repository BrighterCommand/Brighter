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
using System.Threading.Tasks;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox
{
    [Trait("Category", "Sqlite")]
    public class SqliteInboxAddMessageTests : IAsyncDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteInbox _sqlInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private MyCommand _storedCommand;

        public SqliteInboxAddMessageTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupCommandDb();

            _sqlInbox = new SqliteInbox(_sqliteTestHelper.InboxConfiguration);
            _raisedCommand = new MyCommand {Value = "Test"};
            _contextKey = "context-key";
            _sqlInbox.Add(_raisedCommand, _contextKey);
        }

        [Fact]
        public void When_Writing_A_Message_To_The_Inbox()
        {
            _storedCommand = _sqlInbox.Get<MyCommand>(_raisedCommand.Id, _contextKey);

            //_should_read_the_command_from_the__sql_inbox
            AssertionExtensions.Should(_storedCommand).NotBeNull();
            //_should_read_the_command_value
            AssertionExtensions.Should(_storedCommand.Value).Be(_raisedCommand.Value);
            //_should_read_the_command_id
            AssertionExtensions.Should(_storedCommand.Id).Be(_raisedCommand.Id);
        }

        public async ValueTask DisposeAsync()
        {
           await _sqliteTestHelper.CleanUpDbAsync(); 
        }
    }
}
