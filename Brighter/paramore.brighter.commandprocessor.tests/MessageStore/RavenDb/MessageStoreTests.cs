#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using FakeItEasy;

using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Tests.Helpers;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messagestore.ravendb;

namespace paramore.commandprocessor.tests.MessageStore.RavenDb
{
    public class MessageStoreTests : RavenTestBase
    {
        private Establish _context = () =>
        {
            s_documentStore = new EmbeddableDocumentStore().Initialize();
            var logger = A.Fake<ILog>();
            s_messageStore = new RavenMessageStore(s_documentStore, logger);
        };
        public class when_writing_a_message_to_the_raven_message_store
        {
            private Establish _context = () =>
            {
                var messageHeader = new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND);
                messageHeader.Bag.Add(key1, value1);
                messageHeader.Bag.Add(key2, value2);

                s_message = new Message(messageHeader, new MessageBody("Body"));
                s_messageStore.Add(s_message).Wait();
            };

            private Because _of = () => { s_retrievedMessage = s_messageStore.Get(s_message.Id).Result; };

            private It _should_read_the_message_from_the__raven_message_store = () => s_retrievedMessage.ShouldEqual(s_message);
            private It _should_read_the_message_header_first_bag_item_from_the__sql_message_store = () =>
            {
                s_retrievedMessage.Header.Bag.ContainsKey(key1).ShouldBeTrue();
                s_retrievedMessage.Header.Bag[key1].ShouldEqual(value1);
            };
            private It _should_read_the_message_header_second_bag_item_from_the__sql_message_store = () =>
            {
                s_retrievedMessage.Header.Bag.ContainsKey(key2).ShouldBeTrue();
                s_retrievedMessage.Header.Bag[key2].ShouldEqual(value2);
            };
            private static Message s_retrievedMessage;

            private static string key1 = "name1";
            private static string key2 = "name2";
            private static string value1 = "value1";
            private static string value2 = "value2";
        }

        public class when_there_is_no_message_in_the_raven_message_store
        {
            private Establish _context = () => { s_message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body")); };
            private Because _of = () => { s_retrievedMessage = s_messageStore.Get(s_message.Id).Result; };
            private It _should_return_a_empty_message = () => s_retrievedMessage.Header.MessageType.ShouldEqual(MessageType.MT_NONE);
            private static Message s_retrievedMessage;
        }

        public class when_writing_messages_to_the_raven_message_store
        {
            private Establish _context = () =>
            {
                Clock.OverrideTime = DateTime.UtcNow.AddHours(-3);
                
                s_message = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND), new MessageBody("Body"));
                s_messageStore.Add(s_message).Wait();

                Clock.OverrideTime = DateTime.UtcNow.AddHours(-2);
                s_message2 = new Message(new MessageHeader(Guid.NewGuid(), "Test2", MessageType.MT_COMMAND), new MessageBody("Body2"));
                s_messageStore.Add(s_message2).Wait();

                Clock.OverrideTime = DateTime.UtcNow.AddHours(-1);
                s_message3 = new Message(new MessageHeader(Guid.NewGuid(), "Test3", MessageType.MT_COMMAND), new MessageBody("Body3"));
                s_messageStore.Add(s_message3).Wait();
            };

            private Because _of = () => { s_retrievedMessages = s_messageStore.Get().Result; };

            private It _should_read_the_messages_from_the__raven_message_store = () => s_retrievedMessages.Count().ShouldEqual(3);
            private It _should_read_last_message_first_from_the__raven_message_store = () => s_retrievedMessages.First().Id.ShouldEqual(s_message3.Id);
            private It _should_read_first_message_last_from_the__raven_message_store = () => s_retrievedMessages.Last().Id.ShouldEqual(s_message.Id);
            private static Message s_message2;
            private static Message s_message3;
        }
        
        private Cleanup _cleanup = () => s_documentStore.Dispose();

        private static RavenMessageStore s_messageStore;
        private static Message s_message;
        private static IEnumerable<Message> s_retrievedMessages;
        private static IDocumentStore s_documentStore;
    }
}
