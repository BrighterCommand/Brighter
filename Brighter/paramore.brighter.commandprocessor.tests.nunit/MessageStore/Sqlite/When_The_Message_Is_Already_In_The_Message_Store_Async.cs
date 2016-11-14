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
using nUnitShouldAdapter;
using Nito.AsyncEx;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.sqlite;

namespace paramore.brighter.commandprocessor.tests.nunit.messagestore.sqlite
{
    [Subject(typeof(SqliteMessageStore))]
    public class When_The_Message_Is_Already_In_The_Message_Store_Async : ContextSpecification
    {
        private static SqliteTestHelper _sqliteTestHelper;
        private static SqliteMessageStore _sSqlMessageStore;
        private static Exception s_exception;
        private static Message s_messageEarliest;

        private Cleanup _cleanup = () => CleanUpDb();

        private Establish _context = () =>
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupMessageDb();
            _sSqlMessageStore = new SqliteMessageStore(new SqliteMessageStoreConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName_Messages), new LogProvider.NoOpLogger());
            s_messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT),
                new MessageBody("message body"));
            AsyncContext.Run(async () => await _sSqlMessageStore.AddAsync(s_messageEarliest));
        };

        private Because _of = () =>  s_exception = Catch.Exception(() => AsyncContext.Run(async () => await _sSqlMessageStore.AddAsync(s_messageEarliest)));
        private It _should_ignore_the_duplcate_key_and_still_succeed = () => { s_exception.ShouldBeNull(); };

        private static void CleanUpDb()
        {
            _sqliteTestHelper.CleanUpDb();
        }
    }
}
