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
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.Inbox.MySql
{
    [Trait("Category", "MySql")]
    [Collection("MySql Inbox")]
    public class SqlInboxEmptyWhenSearchedTests : IDisposable
    {
        private readonly MySqlTestHelper _mysqlTestHelper;
        private readonly MySqlInbox _mysqlInBox;
        private readonly string _contextKey;

        public SqlInboxEmptyWhenSearchedTests()
        {
            _mysqlTestHelper = new MySqlTestHelper();
            _mysqlTestHelper.SetupCommandDb();

            _mysqlInBox = new MySqlInbox(_mysqlTestHelper.InboxConfiguration);
            _contextKey = "test-context";
        }

        [Fact]
        public void When_There_Is_No_Message_In_The_Sql_Inbox_Get()
        {
            Guid commandId = Guid.NewGuid();
            var exception = Catch.Exception(() => _mysqlInBox.Get<MyCommand>(commandId, _contextKey));
            exception.Should().BeOfType<RequestNotFoundException<MyCommand>>();
        }

        [Fact]
        public void When_There_Is_No_Message_In_The_Sql_Command_Store_Exists()
        {
            Guid commandId = Guid.NewGuid();
            _mysqlInBox.Exists<MyCommand>(commandId, _contextKey).Should().BeFalse();
        }

        public void Dispose()
        {
            _mysqlTestHelper.CleanUpDb();
        }
    }
}
