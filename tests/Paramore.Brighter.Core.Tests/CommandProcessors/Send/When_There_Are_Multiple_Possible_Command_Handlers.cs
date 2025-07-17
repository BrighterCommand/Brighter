using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Send
{
    [Collection("CommandProcessor")]
    public class CommandProcessorSendWithMultipleMatchesTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly Dictionary<string, string> _receivedMessages = new();
        private readonly MyCommand _myCommand = new();
        private Exception _exception;

        public CommandProcessorSendWithMultipleMatchesTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            registry.Register<MyCommand, MyImplicitHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyCommandHandler>(_ => new MyCommandHandler(_receivedMessages));
            container.AddTransient<MyImplicitHandler>();
            container.AddTransient<MyLoggingHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_There_Are_Multiple_Possible_Command_Handlers()
        {
            _exception = Catch.Exception(() => _commandProcessor.Send(_myCommand));


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
