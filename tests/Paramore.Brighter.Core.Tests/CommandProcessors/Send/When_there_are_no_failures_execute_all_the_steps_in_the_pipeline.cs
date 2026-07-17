using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Send
{
    public class CommandProcessorPipelineStepsTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        public CommandProcessorPipelineStepsTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyStepsPreAndPostDecoratedHandler>();
            var container = new ServiceCollection();
            container.AddTransient<MyStepsPreAndPostDecoratedHandler>();
            container.AddTransient<MyStepsValidationHandler<MyCommand>>();
            container.AddTransient<MyStepsLoggingHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_There_Are_No_Failures_Execute_All_The_Steps_In_The_Pipeline()
        {
            _commandProcessor.Send(_myCommand);
            // Should call the pre-validation handler
            await Assert.That(MyStepsValidationHandler<MyCommand>.ShouldReceive(_myCommand)).IsTrue();
            await Assert.That(MyStepsPreAndPostDecoratedHandler.ShouldReceive(_myCommand)).IsTrue();
            // Should call the post validation handler
            await Assert.That(MyStepsLoggingHandler<MyCommand>.ShouldReceive(_myCommand)).IsTrue();
        }
    }
}