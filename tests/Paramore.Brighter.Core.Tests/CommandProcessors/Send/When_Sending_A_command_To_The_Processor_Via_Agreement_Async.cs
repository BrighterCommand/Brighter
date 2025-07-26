using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Send
{
    [Collection("CommandProcessor")]
    public class CommandProcessorSendViaAgreementAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand; 
        private readonly MyCommandHandlerAsync _myCommandHandler;

        public CommandProcessorSendViaAgreementAsyncTests ()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand>((request, context) =>
                {
                    var command = request as MyCommand;
                    if (command.Value == "new")
                        return [typeof(MyCommandHandlerAsync)];
                    
                    return [typeof(MyObsoleteCommandHandlerAsync)];
                    
                }, [typeof(MyCommandHandlerAsync), typeof(MyObsoleteCommandHandlerAsync)]
            );
            _myCommandHandler = new MyCommandHandlerAsync(new Dictionary<string, string>());
            var handlerFactory = new SimpleHandlerFactoryAsync(_ => _myCommandHandler);

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
                new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            
            _myCommand = new MyCommand {Value = "new"};
        }

        [Fact]
        public async Task When_Sending_A_Command_To_The_Processor()
        {
            await _commandProcessor.SendAsync(_myCommand);

            Assert.True(_myCommandHandler.ShouldReceive(_myCommand));

        }

        public void Dispose()
        {
           CommandProcessor.ClearServiceBus(); 
        }
    }
}
