using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.Builders;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Sweeper
{
    [Trait("Category", "InMemory")]
    public class SweeperTests
    {
        [Fact]
        public async Task When_outstanding_in_outbox_sweep_clears_them()
        {
            //Arrange
            const int milliSecondsSinceSent = 500;

            var timeProvider = new FakeTimeProvider();
            var outbox = new InMemoryOutbox(timeProvider);
            var commandProcessor = new FakeCommandProcessor(timeProvider);
            var sweeper = new OutboxSweeper(milliSecondsSinceSent, commandProcessor, new InMemoryRequestContextFactory());

            var messages = new Message[]
            {
                new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()
            };

            foreach (var message in messages)
            {
                outbox.Add(message, new RequestContext());
                commandProcessor.Post(message.ToStubRequest());
            }

            //Act
            timeProvider.Advance(TimeSpan.FromMilliseconds(1000)); // -- let the messages expire

            sweeper.Sweep();

            await Task.Delay(200); //Give the sweep time to run

            //Assert
            outbox.EntryCount.Should().Be(3);
            commandProcessor.Dispatched.Count.Should().Be(3);
            commandProcessor.Deposited.Count.Should().Be(3);
        }

        [Fact]
        public async Task When_outstanding_in_outbox_sweep_clears_them_async()
        {
            //Arrange
            const int milliSecondsSinceSent = 500;

            var timeProvider = new FakeTimeProvider();
            var outbox = new InMemoryOutbox(timeProvider);
            var commandProcessor = new FakeCommandProcessor(timeProvider);
            var sweeper = new OutboxSweeper(milliSecondsSinceSent, commandProcessor, new InMemoryRequestContextFactory());

            var messages = new Message[]
            {
                new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()
            };

            foreach (var message in messages)
            {
                outbox.Add(message, new RequestContext());
                commandProcessor.Post(message.ToStubRequest());
            }

            //Act
            timeProvider.Advance(TimeSpan.FromMilliseconds(milliSecondsSinceSent * 2)); // -- let the messages expire

            sweeper.SweepAsyncOutbox();

            await Task.Delay(200); //Give the sweep time to run

            //Assert
            outbox.EntryCount.Should().Be(3);
            commandProcessor.Dispatched.Count.Should().Be(3);
            commandProcessor.Deposited.Count.Should().Be(3);
        }

        [Fact]
        public async Task When_too_new_to_sweep_leaves_them()
        {
            //Arrange
            const int milliSecondsSinceSent = 500;

            var timeProvider = new FakeTimeProvider();
            var commandProcessor = new FakeCommandProcessor(timeProvider);
            var sweeper = new OutboxSweeper(milliSecondsSinceSent, commandProcessor, new InMemoryRequestContextFactory());

            Message oldMessage = new MessageTestDataBuilder();
            commandProcessor.DepositPost(oldMessage.ToStubRequest());

            var messages = new Message[]
            {
                new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()
            };

            //Thread.Sleep(milliSecondsSinceSent * 2);
            timeProvider.Advance(
                TimeSpan.FromMilliseconds(milliSecondsSinceSent * 2)); //-- allow the messages to be old enough to sweep

            foreach (var message in messages)
            {
                commandProcessor.DepositPost(message.ToStubRequest());
            }

            //Act
            sweeper.Sweep();

            await Task.Delay(200); //Give the sweep time to run

            //Assert
            commandProcessor.Dispatched.Count.Should().Be(1);
            commandProcessor.Deposited.Count.Should().Be(4);
        }

        [Fact]
        public async Task When_too_new_to_sweep_leaves_them_async()
        {
            //Arrange
            const int milliSecondsSinceSent = 500;

            var timeProvider = new FakeTimeProvider();
            var commandProcessor = new FakeCommandProcessor(timeProvider);
            var sweeper = new OutboxSweeper(milliSecondsSinceSent, commandProcessor, new InMemoryRequestContextFactory());

            Message oldMessage = new MessageTestDataBuilder();
            commandProcessor.DepositPost(oldMessage.ToStubRequest());

            var messages = new Message[]
            {
                new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()
            };

            timeProvider.Advance(
                TimeSpan.FromMilliseconds(milliSecondsSinceSent * 2)); //-- allow the messages to be old enough to sweep

            foreach (var message in messages)
            {
                commandProcessor.DepositPost(message.ToStubRequest());
            }

            //Act
            sweeper.SweepAsyncOutbox();

            await Task.Delay(200); //Give the sweep time to run

            //Assert
            commandProcessor.Deposited.Count.Should().Be(4);
            commandProcessor.Dispatched.Count.Should().Be(1);
        }
    }
}
