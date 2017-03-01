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
using Microsoft.Data.Sqlite;
using Nito.AsyncEx;
using NUnit.Framework;
using paramore.brighter.commandprocessor.commandstore.sqlite;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.tests.nunit.commandstore.sqlite
{
    [TestFixture]
    public class SqliteCommandStoreEmptyWhenSearchedAsyncTests
    {
        private SqliteTestHelper _sqliteTestHelper;
        private SqliteCommandStore _sqlCommandStore;
        private MyCommand _storedCommand;
        private SqliteConnection _sqliteConnection;

        [SetUp]
        public void Establish()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteConnection = _sqliteTestHelper.SetupCommandDb();

            _sqlCommandStore = new SqliteCommandStore(new SqliteCommandStoreConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName));
        }

        [Test]
        public void When_There_Is_No_Message_In_The_Sql_Command_Store_Async()
        {
            _storedCommand = AsyncContext.Run<MyCommand>(async () => await _sqlCommandStore.GetAsync<MyCommand>(Guid.NewGuid()));

            //_should_return_an_empty_command_on_a_missing_command
            Assert.AreEqual(Guid.Empty, _storedCommand.Id);
        }

        [TearDown]
        public void Cleanup()
        {
            _sqliteTestHelper.CleanUpDb();
        }
    }
}
