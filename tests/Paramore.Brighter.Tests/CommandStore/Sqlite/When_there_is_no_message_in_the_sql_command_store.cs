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
using Microsoft.Data.Sqlite;
using Xunit;
using Paramore.Brighter.CommandStore.Sqlite;
using Paramore.Brighter.Tests.TestDoubles;

namespace Paramore.Brighter.Tests.commandstore.sqlite
{
    public class SqliteCommandStoreEmptyWhenSearchedTests : IDisposable
    {
        private SqliteTestHelper _sqliteTestHelper;
        private SqliteCommandStore _sqlCommandStore;
        private MyCommand _storedCommand;
        private static SqliteConnection _sqliteConnection;

        public SqliteCommandStoreEmptyWhenSearchedTests()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteConnection = _sqliteTestHelper.SetupCommandDb();
            _sqlCommandStore = new SqliteCommandStore(new SqliteCommandStoreConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName));
        }

        [Fact]
        public void When_There_Is_No_Message_In_The_Sql_Command_Store()
        {
            _storedCommand = _sqlCommandStore.Get<MyCommand>(Guid.NewGuid());

           //_should_return_an_empty_command_on_a_missing_command
            _storedCommand.Id.Should().Be(Guid.Empty);
        }

        public void Dispose()
        {
            _sqliteTestHelper.CleanUpDb();
        }
    }
}