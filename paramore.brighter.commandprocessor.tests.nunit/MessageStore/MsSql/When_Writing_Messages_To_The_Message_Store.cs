#region Licence

/* The MIT License (MIT)
Copyright � 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nUnitShouldAdapter;
using NUnit.Framework;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.time;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageStore.MsSql
{
    [Category("MSSQL")]
    [Subject(typeof(MsSqlMessageStore))]
    public class When_Writing_Messages_To_The_Message_Store : ContextSpecification
    {
        private static MsSqlTestHelper _msSqlTestHelper;
        private static Message s_message2;
        private static Message s_messageEarliest;
        private static Message s_messageLatest;
        private static IEnumerable<Message> s_retrievedMessages;
        private static MsSqlMessageStore s_sqlMessageStore;

        private Cleanup _cleanup = () => CleanUpDb();

        private Establish _context = () =>
        {
            _msSqlTestHelper = new MsSqlTestHelper();
            _msSqlTestHelper.SetupMessageDb();

            s_sqlMessageStore = new MsSqlMessageStore(_msSqlTestHelper.MessageStoreConfiguration);
            Clock.OverrideTime = DateTime.UtcNow.AddHours(-3);
            s_messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND),
                new MessageBody("Body"));
            s_sqlMessageStore.Add(s_messageEarliest);

            Clock.OverrideTime = DateTime.UtcNow.AddHours(-2);

            s_message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND),
                new MessageBody("Body2"));
            s_sqlMessageStore.Add(s_message2);

            Clock.OverrideTime = DateTime.UtcNow.AddHours(-1);

            s_messageLatest = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND),
                new MessageBody("Body3"));
            s_sqlMessageStore.Add(s_messageLatest);
        };

        private Because _of = () => { s_retrievedMessages = s_sqlMessageStore.Get(); };

        private It _should_read_first_message_last_from_the__message_store =
            () => s_retrievedMessages.Last().Id.ShouldEqual(s_messageEarliest.Id);

        private It _should_read_last_message_first_from_the__message_store =
            () => s_retrievedMessages.First().Id.ShouldEqual(s_messageLatest.Id);

        private It _should_read_the_messages_from_the__message_store = () => s_retrievedMessages.Count().ShouldEqual(3);

        private static void CleanUpDb()
        {
            _msSqlTestHelper.CleanUpDb();
        }

    }
}