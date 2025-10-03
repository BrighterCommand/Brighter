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
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox.Binary
{
    [Trait("Category", "PostgresSql")]
    public class SqlInboxDuplicateMessageTests : IDisposable
    {
        private readonly PostgresSqlTestHelper _pgTestHelper;
        private readonly PostgreSqlInbox _pgSqlInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private Exception? _exception;

        public SqlInboxDuplicateMessageTests()
        {
            _pgTestHelper = new PostgresSqlTestHelper(true);
            _pgTestHelper.SetupCommandDb();

            _pgSqlInbox = new PostgreSqlInbox(_pgTestHelper.InboxConfiguration);
            _raisedCommand = new MyCommand { Value = "Test" };
            _contextKey = Guid.NewGuid().ToString();
        }

        [Fact]
        public void When_The_Message_Is_Already_In_The_Inbox()
        {
            _pgSqlInbox.Add(_raisedCommand, _contextKey, null, -1);

            _exception = Catch.Exception(() => _pgSqlInbox.Add(_raisedCommand, _contextKey, null, -1));

            //_should_succeed_even_if_the_message_is_a_duplicate
            Assert.Null(_exception);
            Assert.True(_pgSqlInbox.Exists<MyCommand>(_raisedCommand.Id, _contextKey, null, -1));
        }

        [Fact]
        public void When_The_Message_Is_Already_In_The_Inbox_Different_Context()
        {
            _pgSqlInbox.Add(_raisedCommand, _contextKey, null, -1);

            var newcontext = Guid.NewGuid().ToString();
            _pgSqlInbox.Add(_raisedCommand, newcontext, null, -1);

            var storedCommand = _pgSqlInbox.Get<MyCommand>(_raisedCommand.Id, newcontext, null, -1);

            //Should read the command from the dynamo db inbox
            Assert.NotNull(storedCommand);
        }

        public void Dispose()
        {
            _pgTestHelper.CleanUpDb();
        }
    }
}
