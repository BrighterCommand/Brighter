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
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox
{
    [Trait("Category", "PostgresSql")]
    public class  SqlInboxEmptyWhenSearchedAsyncTests : IDisposable
    {
        private readonly PostgresSqlTestHelper _pgTestHelper;
        private readonly PostgreSqlInbox _sqlSqlInbox;

        public SqlInboxEmptyWhenSearchedAsyncTests()
        {
            _pgTestHelper = new PostgresSqlTestHelper();
            _pgTestHelper.SetupCommandDb();

            _sqlSqlInbox = new PostgreSqlInbox(_pgTestHelper.InboxConfiguration);
        }

        [Fact]
        public async Task When_There_Is_No_Message_In_The_Sql_Inbox_And_I_Get_Async()
        {
            string commandId = Guid.NewGuid().ToString();
            var exception = await Catch.ExceptionAsync(() => _sqlSqlInbox.GetAsync<MyCommand>(commandId, "some-key"));
            Assert.IsType<RequestNotFoundException<MyCommand>>(exception);
            
        }

        [Fact]
        public async Task When_There_Is_No_Message_In_The_Sql_Inbox_And_I_Check_Exists_Async()
        {
            string commandId = Guid.NewGuid().ToString();
            bool exists = await _sqlSqlInbox.ExistsAsync<MyCommand>(commandId, "some-key");
            Assert.False(exists);
        }

        public void Dispose()
        {
            _pgTestHelper.CleanUpDb();
        }
    }
}
