using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.EventStore;
using Xunit;

namespace Paramore.Brighter.Tests.OutBox.EventStore
{
    public class EventStoreOutboxMarkDispatchedAsyncTests : EventStoreFixture
    {
        [Fact]
        public async Task When_marking_a_message_as_dispatched_async_tests()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var messageToMarkAsDispatched = CreateMessage(0, StreamName);
            var dispatchedAt = DateTime.UtcNow;
            var args = new Dictionary<string, object> {{EventStoreOutbox.StreamArg, StreamName}};
            
            await eventStoreOutbox.AddAsync(messageToMarkAsDispatched);

            // act
            await eventStoreOutbox.MarkDispatchedAsync(messageToMarkAsDispatched.Id, dispatchedAt, args);

            // assert
            var messages = await eventStoreOutbox.GetAsync(1, 2, args);

            messages.Should().ContainSingle().Which.Header.Bag[EventStoreOutbox.DispatchedAtKey].Should().Be(dispatchedAt);
        }
        
        [Fact]
        public void When_null_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.MarkDispatchedAsync(Guid.Empty, DateTime.UtcNow);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void When_null_stream_arg_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var args = new Dictionary<string, object> {{EventStoreOutbox.StreamArg, null}};
            
            // act
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.MarkDispatchedAsync(Guid.Empty, DateTime.UtcNow, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void When_empty_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var args = new Dictionary<string, object>();
            
            // act
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.MarkDispatchedAsync(Guid.Empty, DateTime.UtcNow, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void When_wrong_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var args = new Dictionary<string, object> { { "Foo", "Bar" }};
            
            // act
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.MarkDispatchedAsync(Guid.Empty, DateTime.UtcNow, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }
    }
}
