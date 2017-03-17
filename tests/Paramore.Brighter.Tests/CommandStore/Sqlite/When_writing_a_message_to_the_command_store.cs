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

using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Paramore.Brighter.CommandStore.Sqlite;
using Paramore.Brighter.Tests.TestDoubles;

namespace Paramore.Brighter.Tests.commandstore.sqlite
{
    [TestFixture]
    public class SqliteCommandStoreAddMessageTests
    {
        private SqliteTestHelper _sqliteTestHelper;
        private SqliteCommandStore _sqlCommandStore;
        private MyCommand _raisedCommand;
        private MyCommand _storedCommand;
        private SqliteConnection _sqliteConnection;

        [SetUp]
        public void Establish()
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteConnection = _sqliteTestHelper.SetupCommandDb();

            _sqlCommandStore = new SqliteCommandStore(new SqliteCommandStoreConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName));
            _raisedCommand = new MyCommand() {Value = "Test"};
            _sqlCommandStore.Add<MyCommand>(_raisedCommand);
        }

        [Test]
        public void When_Writing_A_Message_To_The_Command_Store()
        {
            _storedCommand = _sqlCommandStore.Get<MyCommand>(_raisedCommand.Id);

            //_should_read_the_command_from_the__sql_command_store
            Assert.NotNull(_storedCommand);
            //_should_read_the_command_value
            Assert.AreEqual(_raisedCommand.Value, _storedCommand.Value);
            //_should_read_the_command_id
            Assert.AreEqual(_raisedCommand.Id, _storedCommand.Id);
        }

        [TearDown]
        public void Cleanup()
        {
            _sqliteTestHelper.CleanUpDb();
        }
    }
}