#region Licence
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
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.Inbox.MsSql
{
    [Trait("Category", "MSSQL")]
    [Collection("MSSQL Inbox")]
    public class  SqlInboxEmptyWhenSearchedAsyncTests : IDisposable
    {
        private readonly MsSqlTestHelper _msSqlTestHelper;
        private readonly MsSqlInbox _sqlInbox;

        public SqlInboxEmptyWhenSearchedAsyncTests()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupCommandDb();

            _sqlInbox = new MsSqlInbox(_msSqlTestHelper.InboxConfiguration);
        }

        [Fact]
        public async Task When_There_Is_No_Message_In_The_Sql_Inbox_And_I_Get_Async()
        {
            Guid commandId = Guid.NewGuid();
            var exception = await Catch.ExceptionAsync(() => _sqlInbox.GetAsync<MyCommand>(commandId, "some-key"));
            exception.Should().BeOfType<RequestNotFoundException<MyCommand>>();
        }

        [Fact]
        public async Task When_There_Is_No_Message_In_The_Sql_Inbox_And_I_Check_Exists_Async()
        {
            Guid commandId = Guid.NewGuid();
            bool exists = await _sqlInbox.ExistsAsync<MyCommand>(commandId, "some-key");
            exists.Should().BeFalse();
        }

        public void Dispose()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}
