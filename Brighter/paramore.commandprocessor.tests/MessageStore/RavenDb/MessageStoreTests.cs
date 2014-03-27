using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            using (var store = NewDocumentStore())
            {
                var messageStore = new RavenMessageStore(store);
                //act
                var message = new Message(new MessageHeader(Guid.NewGuid(), "Test"), new MessageBody("Body"));               
                messageStore.Add(message);
                var retrievedMessage = messageStore.Get(message.Id);

                //assert
               Assert.IsTrue(message == retrievedMessage); 
            }
        }
    }
}
