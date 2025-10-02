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
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox.Text
{
    [Trait("Category", "PostgresSql")]
    public class SqlInboxDuplicateMessageAsyncTests : IDisposable
    {
        private readonly PostgresSqlTestHelper _pgTestHelper;
        private readonly PostgreSqlInbox _pgSqlInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private Exception? _exception;

        public SqlInboxDuplicateMessageAsyncTests()
        {
            _pgTestHelper = new PostgresSqlTestHelper();
            _pgTestHelper.SetupCommandDb();

            _pgSqlInbox = new PostgreSqlInbox(_pgTestHelper.InboxConfiguration);
            _raisedCommand = new MyCommand { Value = "Test" };
            _contextKey = "test-context";
        }

        [Fact]
        public async Task When_The_Message_Is_Already_In_The_Inbox_Async()
        {
            await _pgSqlInbox.AddAsync(_raisedCommand, _contextKey, null, -1, default);

            _exception = await Catch.ExceptionAsync(() => _pgSqlInbox.AddAsync(_raisedCommand, _contextKey, null, -1, default));

            //_should_succeed_even_if_the_message_is_a_duplicate
            Assert.Null(_exception);
            var exists = await _pgSqlInbox.ExistsAsync<MyCommand>(_raisedCommand.Id, _contextKey, null, -1, default);
            Assert.True(exists);
        }

        [Fact]
        public async Task When_The_Message_Is_Already_In_The_Inbox_Different_Context()
        {
            await _pgSqlInbox.AddAsync(_raisedCommand, "some other key", null, -1, default);

            var storedCommand = _pgSqlInbox.Get<MyCommand>(_raisedCommand.Id, "some other key", null, -1);

            //Should read the command from the dynamo db inbox
            Assert.NotNull(storedCommand);
        }

        public void Dispose()
        {
            _pgTestHelper.CleanUpDb();
        }
    }
}
