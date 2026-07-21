using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class PipelineTerminationTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private Exception _exception;
        public PipelineTerminationTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyUnusedCommandHandler>();
            var container = new ServiceCollection();
            container.AddTransient<MyAbortingHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        public async Task When_An_Exception_Is_Thrown_Terminate_The_Pipeline()
        {
            _exception = Catch.Exception(() => _commandProcessor.Send(_myCommand));
            await Assert.That(_exception).IsNotNull();
            await Assert.That(MyUnusedCommandHandler.Shouldreceive(_myCommand)).IsFalse();
        }
    }
}