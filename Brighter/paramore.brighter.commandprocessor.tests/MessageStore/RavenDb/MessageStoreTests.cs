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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Raven.Client.Embedded;
using Raven.Tests.Helpers;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messagestore.ravendb;

namespace paramore.commandprocessor.tests.MessageStore.RavenDb
{
    [TestClass]
    public class MessageStoreTests : RavenTestBase
    {
        [TestMethod]
        public void Writing_and_reading_a_message_from_the_store()
        {
            //arrange
            using (var store = new EmbeddableDocumentStore().Initialize())
            {
                var logger = A.Fake<ILog>();
                var messageStore = new RavenMessageStore(store, logger);
                //act
                var message = new Message(new MessageHeader(Guid.NewGuid(), "Test", MessageType.MT_COMMAND), new MessageBody("Body"));               
                messageStore.Add(message).Wait();
                var retrievedMessage = messageStore.Get(message.Id).Result;

                //assert
               Assert.IsTrue(message == retrievedMessage); 
            }
        }
    }
}
