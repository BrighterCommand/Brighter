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
using Common.Logging;
using FakeItEasy;

using Machine.Specifications;

using Raven.Client;
using Raven.Client.Embedded;
using Raven.Tests.Helpers;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messagestore.ravendb;

namespace paramore.commandprocessor.tests.MessageStore.RavenDb
{

    public class MessageStoreTests : RavenTestBase
    {
        Establish context = () =>
        {
            documentStore = new EmbeddableDocumentStore().Initialize();
            var logger = A.Fake<ILog>();
            messageStore = new RavenMessageStore(documentStore, logger);
        };
        public class when_writing_a_message_to_the_raven_message_store
        {
            Establish context = () =>
            {
                message = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND), new MessageBody("Body"));
                messageStore.Add(message).Wait();
            };

            Because of = () => { retrievedMessage = messageStore.Get(message.Id).Result; };

            It should_read_the_message_from_the__raven_message_store = () => retrievedMessage.ShouldEqual(message);
        }

        public class when_there_is_no_message_in_the_raven_message_store
        {
            Establish context = () => { message = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body")); };
            Because of = () => { retrievedMessage = messageStore.Get(message.Id).Result; };
            It should_return_a_empty_message = () => retrievedMessage.Header.MessageType.ShouldEqual(MessageType.MT_NONE);
        }

        Cleanup cleanup = () => documentStore.Dispose();

        private static RavenMessageStore messageStore;
        private static Message message;
        private static Message retrievedMessage;
        private static IDocumentStore documentStore;
        
    }
}
