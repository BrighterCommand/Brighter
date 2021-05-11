﻿#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.MySQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Inbox
{
    [Trait("Category", "MySql")]
    public class SqlInboxDuplicateMessageAsyncTests : IDisposable
    {
        private readonly MySqlTestHelper _mysqlTestHelper;
        private readonly MySqlInbox _mysqlInbox;
        private readonly MyCommand _raisedCommand;
        private readonly string _contextKey;
        private Exception _exception;

        public SqlInboxDuplicateMessageAsyncTests()
        {
            _mysqlTestHelper = new MySqlTestHelper();
            _mysqlTestHelper.SetupCommandDb();

            _mysqlInbox = new MySqlInbox(_mysqlTestHelper.InboxConfiguration);
            _raisedCommand = new MyCommand { Value = "Test" };
            _contextKey = "test-context";
        }

        [Fact]
        public async Task When_The_Message_Is_Already_In_The_Inbox_Async()
        {
            await _mysqlInbox.AddAsync(_raisedCommand, _contextKey);

            _exception = await Catch.ExceptionAsync(() => _mysqlInbox.AddAsync(_raisedCommand, _contextKey));

           //_should_succeed_even_if_the_message_is_a_duplicate
            _exception.Should().BeNull();
            var exists = await _mysqlInbox.ExistsAsync<MyCommand>(_raisedCommand.Id, _contextKey);
            exists.Should().BeTrue();
        }

        [Fact]
        public async void When_The_Message_Is_Already_In_The_Inbox_Different_Context()
        {
            await _mysqlInbox.AddAsync(_raisedCommand, "some other key");

            var storedCommand = _mysqlInbox.Get<MyCommand>(_raisedCommand.Id, "some other key");

            //_should_read_the_command_from_the__dynamo_db_inbox
            storedCommand.Should().NotBeNull();
        }

        public void Dispose()
        {
            _mysqlTestHelper.CleanUpDb();
        }
    }
}
