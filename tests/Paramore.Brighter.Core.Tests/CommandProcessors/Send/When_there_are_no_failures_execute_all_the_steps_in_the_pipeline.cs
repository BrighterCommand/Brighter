using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Send
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPipelineStepsTests : IDisposable
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
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_There_Are_No_Failures_Execute_All_The_Steps_In_The_Pipeline()
        {
            _commandProcessor.Send(_myCommand);

            // Should call the pre-validation handler
            Assert.True(MyStepsValidationHandler<MyCommand>.ShouldReceive(_myCommand));
            Assert.True(MyStepsPreAndPostDecoratedHandler.ShouldReceive(_myCommand));
            // Should call the post validation handler
            Assert.True(MyStepsLoggingHandler<MyCommand>.ShouldReceive(_myCommand));
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
