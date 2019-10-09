using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Outbox.EventStore;
using Xunit;

namespace Paramore.Brighter.Tests.OutBox.EventStore
{
    [Trait("Category", "EventStore")]
    [Collection("EventStore Outbox")]
    public class EventStoreOutboxRangeAsyncTests : EventStoreFixture
    {
        [Fact]
        public async Task When_there_are_multiple_messages_in_the_outbox_and_a_range_is_fetched_async()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);

            var message1 = CreateMessage(0, StreamName);
            var message2 = CreateMessage(1, StreamName);
            var message3 = CreateMessage(2, StreamName);
            
            eventStoreOutbox.Add(message1);
            await Task.Delay(100);
            eventStoreOutbox.Add(message2);
            await Task.Delay(100);
            eventStoreOutbox.Add(message3);

            var args = new Dictionary<string, object> {{EventStoreOutbox.StreamArg, StreamName}};
            
            // act
            var messages = await eventStoreOutbox.GetAsync(1, 3, args);
            
            // assert
            messages.Should().ContainSingle().Which.Should().BeEquivalentTo(message3);
        }

        [Fact]
        public async Task When_null_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.GetAsync(1, 1);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task When_null_stream_arg_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var args = new Dictionary<string, object> {{EventStoreOutbox.StreamArg, null}};
            
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.GetAsync(1, 1, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }

        [Fact]
        public async Task When_empty_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var args = new Dictionary<string, object>();
            
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.GetAsync(1, 1, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }

        [Fact]
        public async Task When_wrong_args_are_supplied()
        {
            // arrange
            var eventStoreOutbox = new EventStoreOutbox(Connection);
            var args = new Dictionary<string, object> { { "Foo", "Bar" }};
            
            // act
            Func<Task> getWithoutArgs = async () => await eventStoreOutbox.GetAsync(1, 1, args);
            
            // assert
            getWithoutArgs.Should().Throw<ArgumentException>();
        }
    }
}
