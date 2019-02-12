using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.EventSourcing.TestDoubles;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.EventSourcing
{
    public class OnceOnlyAttributeTests 
    {
        private readonly MyCommand _command;
        private readonly IAmACommandStore _commandstore;
        private readonly IAmACommandProcessor _commandProcessor;
        
        public OnceOnlyAttributeTests()
        {
            _commandstore = new InMemoryCommandStore();
            
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyStoredCommandHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyStoredCommandHandler>();
            container.Register(_commandstore);

            _command = new MyCommand {Value = "My Test String"};

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
 
        }

        [Fact]
        public void When_Handling_A_Command_Only_Once()
        {
            _commandProcessor.Send(_command);

            var exception = Record.Exception(() => _commandProcessor.Send(_command));
            
            Assert.Null(exception);
        }
    }
}
