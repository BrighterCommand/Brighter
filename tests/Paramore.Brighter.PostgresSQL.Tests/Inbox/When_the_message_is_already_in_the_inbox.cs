﻿#region Licence

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
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox
{
    [Trait("Category", "PostgresSql")]
    public class SqlInboxDuplicateMessageTests : IDisposable
    {
        private readonly PostgresSqlTestHelper _pgTestHelper;
        private readonly PostgresSqlInbox _pgSqlInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private Exception _exception;

        public SqlInboxDuplicateMessageTests()
        {
            _pgTestHelper = new PostgresSqlTestHelper();
            _pgTestHelper.SetupCommandDb();

            _pgSqlInbox = new PostgresSqlInbox(_pgTestHelper.InboxConfiguration);
            _raisedCommand = new MyCommand { Value = "Test" };
            _contextKey = Guid.NewGuid().ToString();
        }

        [Fact]
        public void When_The_Message_Is_Already_In_The_Inbox()
        {
            _pgSqlInbox.Add(_raisedCommand, _contextKey);
            
            _exception = Catch.Exception(() => _pgSqlInbox.Add(_raisedCommand, _contextKey));

            //_should_succeed_even_if_the_message_is_a_duplicate
            _exception.Should().BeNull();
            _pgSqlInbox.Exists<MyCommand>(_raisedCommand.Id, _contextKey).Should().BeTrue();
        }

        [Fact]
        public void When_The_Message_Is_Already_In_The_Inbox_Different_Context()
        {
            _pgSqlInbox.Add(_raisedCommand, _contextKey);

            var newcontext = Guid.NewGuid().ToString();
            _pgSqlInbox.Add(_raisedCommand, newcontext);

            var storedCommand = _pgSqlInbox.Get<MyCommand>(_raisedCommand.Id, newcontext);

            //_should_read_the_command_from_the__dynamo_db_inbox
            AssertionExtensions.Should((object) storedCommand).NotBeNull();
        }

        public void Dispose()
        {
            _pgTestHelper.CleanUpDb();
        }
    }
}
