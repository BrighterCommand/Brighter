using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.FeatureSwitch.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.FeatureSwitch.Handlers;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.FeatureSwitch
{
    [Collection("CommandProcessor")]
    public class CommandProcessorWithFeatureSwitchOffInPipelineTests : IDisposable
    {
        private readonly MyCommand _myCommand = new();
        private readonly MyCommandAsync _myAsyncCommand = new();

        private readonly CommandProcessor _commandProcessor;

        public CommandProcessorWithFeatureSwitchOffInPipelineTests()
        {
            SubscriberRegistry registry = new();
            registry.Register<MyCommand, MyFeatureSwitchedOffHandler>();
            registry.RegisterAsync<MyCommandAsync, MyFeatureSwitchedOffHandlerAsync>();

            var container = new ServiceCollection();
            container.AddSingleton<MyFeatureSwitchedOffHandler>();
            container.AddSingleton<MyFeatureSwitchedOffHandlerAsync>();
            container.AddTransient<FeatureSwitchHandler<MyCommand>>();
            container.AddTransient<FeatureSwitchHandlerAsync<MyCommandAsync>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            ServiceProviderHandlerFactory handlerFactory = new(container.BuildServiceProvider());
            
            _commandProcessor = CommandProcessorBuilder
                .StartNew()
                .Handlers(new HandlerConfiguration(registry, handlerFactory))
                .DefaultResilience()
                .NoExternalBus()
                .ConfigureInstrumentation(new BrighterTracer(TimeProvider.System), InstrumentationOptions.All)
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .RequestSchedulerFactory(new InMemorySchedulerFactory())
                .Build();
        }

        [Fact]
        public void When_Sending_A_Command_To_The_Processor_When_A_Feature_Switch_Is_Off()
        {
            _commandProcessor.Send(_myCommand);

            Assert.False(MyFeatureSwitchedOffHandler.DidReceive());
        }

        [Fact]
        public async Task When_Sending_A_Async_Command_To_The_Processor_When_A_Feature_Switch_Is_Off()
        {
            await _commandProcessor.SendAsync(_myAsyncCommand);

            Assert.False(MyFeatureSwitchedOffHandlerAsync.DidReceive());
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
