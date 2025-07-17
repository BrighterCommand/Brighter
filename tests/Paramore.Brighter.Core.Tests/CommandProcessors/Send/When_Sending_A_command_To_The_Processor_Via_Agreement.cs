using System;
using System.Collections.Generic;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Send
{
    [Collection("CommandProcessor")]
    public class CommandProcessorSendViaAgreementTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand; 
        private readonly MyCommandHandler _myCommandHandler;

        public CommandProcessorSendViaAgreementTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand>((request, context) =>
                {
                    var command = request as MyCommand;
                    if (command.Value == "new")
                        return [typeof(MyCommandHandler)];
                    
                    return [typeof(MyObsoleteCommandHandler)];
                    
                }, [typeof(MyCommandHandler), typeof(MyObsoleteCommandHandler)]
            );
            _myCommandHandler = new MyCommandHandler(new Dictionary<string, string>());
            var handlerFactory = new SimpleHandlerFactorySync(_ => _myCommandHandler);

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
                new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            
            _myCommand = new MyCommand {Value = "new"};
        }

        [Fact]
        public void When_Sending_A_Command_To_The_Processor()
        {
            _commandProcessor.Send(_myCommand);

            Assert.True(_myCommandHandler.ShouldReceive(_myCommand));

        }

        public void Dispose()
        {
           CommandProcessor.ClearServiceBus(); 
        }
    }
}
