using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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
            
            var outbox = new InMemoryOutbox();
            var commandProcessor = new FakeCommandProcessor();
            var sweeper = new OutboxSweeper(milliSecondsSinceSent, commandProcessor);

            var messages = new Message[] {new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()};

            foreach (var message in messages)
            {
                outbox.Add(message);
                commandProcessor.Post(message.ToStubRequest());
            }

            //Act
            await Task.Delay(1000); // -- let the messages expire
            
            sweeper.Sweep();

            Thread.Sleep(200);

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
            
            var outbox = new InMemoryOutbox();
            var commandProcessor = new FakeCommandProcessor();
            var sweeper = new OutboxSweeper(milliSecondsSinceSent, commandProcessor);

            var messages = new Message[] {new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()};

            foreach (var message in messages)
            {
                outbox.Add(message);
                commandProcessor.Post(message.ToStubRequest());
            }

            //Act
            await Task.Delay(milliSecondsSinceSent * 2); // -- let the messages expire
            
            sweeper.SweepAsyncOutbox();

            await Task.Delay(200);

            //Assert
            outbox.EntryCount.Should().Be(3);
            commandProcessor.Dispatched.Count.Should().Be(3);
            commandProcessor.Deposited.Count.Should().Be(3);

        }

    }
}
