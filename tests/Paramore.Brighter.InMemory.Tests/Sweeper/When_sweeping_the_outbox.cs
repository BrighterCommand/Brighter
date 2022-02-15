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
        public void When_outstanding_in_outbox_sweep_clears_them()
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
            Task.Delay(1000).Wait(); // -- let the messages expire
            
            sweeper.Sweep();
            
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
            Task.Delay(1000).Wait(); // -- let the messages expire
            
            await sweeper.SweepAsync();
            
            //Assert
            outbox.EntryCount.Should().Be(3);
            commandProcessor.Dispatched.Count.Should().Be(3);
            commandProcessor.Deposited.Count.Should().Be(3);

        }

        [Fact]
        public void When_too_new_to_sweep_leaves_them()
        {
             //Arrange
             const int milliSecondsSinceSent = 500;
             
             var commandProcessor = new FakeCommandProcessor();
             var sweeper = new OutboxSweeper(milliSecondsSinceSent, commandProcessor);
             
             Message oldMessage = new MessageTestDataBuilder();
             commandProcessor.DepositPost(oldMessage.ToStubRequest());

             var messages = new Message[] {new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()};
            
             Thread.Sleep(milliSecondsSinceSent);

             foreach (var message in messages)
             {
                 commandProcessor.DepositPost(message.ToStubRequest());
             }

             //Act
             sweeper.Sweep();
             
             //Assert
             commandProcessor.Dispatched.Count.Should().Be(1);
             commandProcessor.Deposited.Count.Should().Be(4);
           
        }
        
        [Fact]
        public async Task When_too_new_to_sweep_leaves_them_async()
        {
            //Arrange
            const int milliSecondsSinceSent = 500;
            
            var commandProcessor = new FakeCommandProcessor();
            var sweeper = new OutboxSweeper(milliSecondsSinceSent, commandProcessor);
 
            Message oldMessage = new MessageTestDataBuilder();
            commandProcessor.DepositPost(oldMessage.ToStubRequest());
            
            var messages = new Message[] {new MessageTestDataBuilder(), new MessageTestDataBuilder(), new MessageTestDataBuilder()};

            await Task.Delay(milliSecondsSinceSent);

            foreach (var message in messages)
            {
                commandProcessor.DepositPost(message.ToStubRequest());
            }
 
            //Act
            await sweeper.SweepAsync();
             
            //Assert
            commandProcessor.Dispatched.Count.Should().Be(1);
            commandProcessor.Deposited.Count.Should().Be(4);
           
        }
        
    }
}
