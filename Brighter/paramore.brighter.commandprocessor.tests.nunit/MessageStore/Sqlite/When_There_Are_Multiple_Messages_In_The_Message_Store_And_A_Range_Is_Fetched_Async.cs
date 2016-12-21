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
using System.Collections.Generic;
using System.Linq;
using nUnitShouldAdapter;
using Nito.AsyncEx;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.messagestore.sqlite;

namespace paramore.brighter.commandprocessor.tests.nunit.messagestore.sqlite
{
    [Subject(typeof(SqliteMessageStore))]
    public class When_There_Are_Multiple_Messages_In_The_Message_Store_And_A_Range_Is_Fetched_Async : ContextSpecification
    {
        private static SqliteTestHelper _sqliteTestHelper;
        private static SqliteMessageStore _sSqlMessageStore;
        private static readonly string _TopicFirstMessage = "test_topic";
        private static readonly string _TopicLastMessage = "test_topic3";
        private static IEnumerable<Message> messages;
        private static Message s_message1;
        private static Message s_message2;
        private static Message s_messageEarliest;

        private Establish _context = () =>
        {
            _sqliteTestHelper = new SqliteTestHelper();
            _sqliteTestHelper.SetupMessageDb();
            _sSqlMessageStore = new SqliteMessageStore(new SqliteMessageStoreConfiguration(_sqliteTestHelper.ConnectionString, _sqliteTestHelper.TableName_Messages));
            s_messageEarliest =
                new Message(new MessageHeader(Guid.NewGuid(), _TopicFirstMessage, MessageType.MT_DOCUMENT),
                    new MessageBody("message body"));
            s_message1 = new Message(new MessageHeader(Guid.NewGuid(), "test_topic2", MessageType.MT_DOCUMENT),
                new MessageBody("message body2"));
            s_message2 = new Message(new MessageHeader(Guid.NewGuid(), _TopicLastMessage, MessageType.MT_DOCUMENT),
                new MessageBody("message body3"));
            AsyncContext.Run( async () => await _sSqlMessageStore.AddAsync(s_messageEarliest));
            AsyncContext.Run( async () => await _sSqlMessageStore.AddAsync(s_message1));
            AsyncContext.Run( async () => await _sSqlMessageStore.AddAsync(s_message2));
        };

        private Because _of = () =>  AsyncContext.Run(async () => messages = await _sSqlMessageStore.GetAsync(1, 3)); 

        private It _should_fetch_1_message = () => messages.Count().ShouldEqual(1);
        private It _should_fetch_expected_message = () => messages.First().Header.Topic.ShouldEqual(_TopicLastMessage);
        private It _should_not_fetch_null_messages = () => messages.ShouldNotBeNull();

        private Cleanup _cleanup = () => _sqliteTestHelper.CleanUpDb();
    }
}
