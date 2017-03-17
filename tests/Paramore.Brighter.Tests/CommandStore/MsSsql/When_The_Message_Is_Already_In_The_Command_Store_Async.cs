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
using Nito.AsyncEx;
using NUnit.Framework;
using Paramore.Brighter.CommandStore.MsSql;
using Paramore.Brighter.Tests.TestDoubles;

namespace Paramore.Brighter.Tests.CommandStore.MsSsql
{

    [Category("MSSQL")]
    [TestFixture]
    public class SqlCommandStoreDuplicateMessageAsyncTests
    {
        private MsSqlTestHelper _msSqlTestHelper;
        private MsSqlCommandStore _sqlCommandStore;
        private MyCommand _raisedCommand;
        private Exception _exception;

        [SetUp]
        public void Establish()
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupCommandDb();

            _sqlCommandStore = new MsSqlCommandStore(_msSqlTestHelper.CommandStoreConfiguration);
            _raisedCommand = new MyCommand {Value = "Test"};
            AsyncContext.Run(async () => await _sqlCommandStore.AddAsync<MyCommand>(_raisedCommand));
        }

        [Test]
        public void When_The_Message_Is_Already_In_The_Command_Store_Async()
        {
            _exception = Catch.Exception(() => AsyncContext.Run(async () => await _sqlCommandStore.AddAsync(_raisedCommand)));

           //_should_succeed_even_if_the_message_is_a_duplicate
            Assert.Null(_exception);
        }

        [TearDown]
        public void Cleanup()
        {
            _msSqlTestHelper.CleanUpDb();
        }
    }
}
