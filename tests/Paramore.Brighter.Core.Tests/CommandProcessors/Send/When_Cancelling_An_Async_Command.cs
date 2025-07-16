using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Send
{
    [Collection("CommandProcessor")]
    public class CancellingAsyncPipelineTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private readonly MyCommand _myCommand = new MyCommand();

        public CancellingAsyncPipelineTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCancellableCommandHandlerAsync>();
            var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(_receivedMessages));

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        //Ignore any errors about adding System.Runtime from the IDE. See https://social.msdn.microsoft.com/Forums/en-US/af4dc0db-046c-4728-bfe0-60ceb93f7b9f/vs2012net-45-rc-compiler-error-when-using-actionblock-missing-reference-to?forum=tpldataflow
        [Fact]
        public async Task When_Cancelling_An_Async_Command()
        {
            await _commandProcessor.SendAsync(_myCommand);

            //Should send the command to the command handler
            Assert.Contains(new KeyValuePair<string, string>(nameof(MyCommandHandlerAsync), _myCommand.Id), _receivedMessages);

        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
