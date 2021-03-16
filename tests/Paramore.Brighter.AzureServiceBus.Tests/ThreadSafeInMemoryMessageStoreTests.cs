using System;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests
{
    public class ThreadSafeInMemoryMessageStoreTests
    {
        [Fact]
        public void When_adding_a_new_message_it_should_add_it()
        {
            var store = new ConcurrentInMemoryMessageStore();
            Message message = new();
            store.Add(message);
            Message storedMessage = store.Get(message.Id);
            Assert.Equal(message, storedMessage);
        }

        [Fact]
        public void When_adding_a_new_message_that_already_exist_it_should_not_override_it()
        {
            var store = new ConcurrentInMemoryMessageStore();
            Message message = new();
            store.Add(message);
            store.Add(message);
            Message storedMessage = store.Get(message.Id);
            Assert.Equal(message, storedMessage);
        }

        [Fact]
        public void When_getting_a_message_that_does_not_exist_it_should_return_null()
        {
            var store = new ConcurrentInMemoryMessageStore();
            Message storedMessage = store.Get(Guid.NewGuid());
            Assert.Null(storedMessage);
        }

        [Fact]
        public void When_getting_a_message_it_should_then_remove_it()
        {
            var store = new ConcurrentInMemoryMessageStore();
            Message message = new();
            store.Add(message);
            Message storedMessage = store.Get(message.Id);
            Assert.NotNull(storedMessage);
            storedMessage = store.Get(message.Id);
            Assert.Null(storedMessage);
        }
    }
}
