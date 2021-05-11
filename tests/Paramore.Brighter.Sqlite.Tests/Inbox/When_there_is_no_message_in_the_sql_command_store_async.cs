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
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Sqlite.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox
{
    [Trait("Category", "Sqlite")]
    public class SqliteInboxEmptyWhenSearchedAsyncTests : IDisposable
    {
        private readonly SqliteTestHelper _sqliteTestHelper;
        private readonly SqliteInbox _sqlInbox;
        private readonly string _contextKey;

        public SqliteInboxEmptyWhenSearchedAsyncTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupCommandDb();

            _sqlInbox = new SqliteInbox(new SqliteInboxConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName));
            _contextKey = "context-key";
        }

        [Fact]
        public async Task When_There_Is_No_Message_In_The_Sql_Inbox_Get_Async()
        {
            Guid commandId = Guid.NewGuid();
            var exception = await Catch.ExceptionAsync(() => _sqlInbox.GetAsync<MyCommand>(commandId, _contextKey));
            AssertionExtensions.Should((object) exception).BeOfType<RequestNotFoundException<MyCommand>>();
        }

        [Fact]
        public async Task When_There_Is_No_Message_In_The_Sql_Inbox_Exists_Async()
        {
            Guid commandId = Guid.NewGuid();
            bool exists = await _sqlInbox.ExistsAsync<MyCommand>(commandId, _contextKey);
            exists.Should().BeFalse();
        }

        public void Dispose()
        {
            _sqliteTestHelper.CleanUpDb();
        }
    }
}
