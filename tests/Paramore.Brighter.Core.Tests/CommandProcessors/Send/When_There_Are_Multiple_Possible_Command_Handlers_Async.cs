using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Send
{
    [Collection("CommandProcessor")]
    public class CommandProcessorSendWithMultipleMatchesAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private readonly MyCommand _myCommand = new MyCommand();
        private Exception? _exception;

        public CommandProcessorSendWithMultipleMatchesAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            registry.RegisterAsync<MyCommand, MyImplicitHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyCommandHandlerAsync>();
            container.AddTransient<MyImplicitHandlerAsync>();
            container.AddTransient<MyLoggingHandlerAsync<MyCommand>>();
            container.AddSingleton(_receivedMessages);
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        //Ignore any errors about adding System.Runtime from the IDE. See https://social.msdn.microsoft.com/Forums/en-US/af4dc0db-046c-4728-bfe0-60ceb93f7b9f/vs2012net-45-rc-compiler-error-when-using-actionblock-missing-reference-to?forum=tpldataflow
        [Fact]
        public async Task When_There_Are_Multiple_Possible_Command_Handlers_Async()
        {
            _exception = await Catch.ExceptionAsync(async () => await _commandProcessor.SendAsync(_myCommand));

            //Should fail because multiple receivers found
            Assert.IsType<ArgumentException>(_exception);
            //Should have an error message that tells you why
            Assert.NotNull(_exception);
            Assert.Contains("More than one handler was found for the typeof command Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles.MyCommand - a command should only have one handler.", _exception.Message);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
