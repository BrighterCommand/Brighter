using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.FeatureSwitch.TestDoubles;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.FeatureSwitch.Providers;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.FeatureSwitch.Handlers;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.FeatureSwitch
{
    [Collection("CommandProcessor")]
    public class CommandProcessorWithFeatureSwitchOnByConfigInPipelineTests : IDisposable
    {
        private readonly MyCommand _myCommand = new();
        private readonly MyCommandAsync _myAsyncCommand = new();

        private readonly CommandProcessor _commandProcessor;
        readonly ServiceProvider _provider;

        public CommandProcessorWithFeatureSwitchOnByConfigInPipelineTests()
        {
            SubscriberRegistry registry = new();
            registry.Register<MyCommand, MyFeatureSwitchedConfigHandler>();
            registry.RegisterAsync<MyCommandAsync, MyFeatureSwitchedConfigHandlerAsync>();

            var container = new ServiceCollection();
            container.AddSingleton<MyFeatureSwitchedConfigHandler>();
            container.AddSingleton<MyFeatureSwitchedConfigHandlerAsync>();
            container.AddTransient<FeatureSwitchHandler<MyCommand>>();
            container.AddTransient<FeatureSwitchHandlerAsync<MyCommandAsync>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
            
            _provider = container.BuildServiceProvider();
            ServiceProviderHandlerFactory handlerFactory = new(_provider);
            
            IAmAFeatureSwitchRegistry fluentConfig = FluentConfigRegistryBuilder
                .With()                
                .StatusOf<MyFeatureSwitchedConfigHandler>().Is(FeatureSwitchStatus.On)
                .StatusOf<MyFeatureSwitchedConfigHandlerAsync>().Is(FeatureSwitchStatus.On)
                .Build();
            
            _commandProcessor = CommandProcessorBuilder
                .StartNew()
                .ConfigureFeatureSwitches(fluentConfig)
                .Handlers(new HandlerConfiguration(registry, handlerFactory))
                .DefaultResilience()
                .NoExternalBus()
                .ConfigureInstrumentation(new BrighterTracer(TimeProvider.System), InstrumentationOptions.All)
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .RequestSchedulerFactory(new InMemorySchedulerFactory())
                .Build();
        }

        [Fact]
        public void When_Sending_A_Command_To_The_Processor_When_A_Feature_Switch_Is_On_By_Fluent_Config()
        {
            _commandProcessor.Send(_myCommand);

            Assert.True(_provider.GetService<MyFeatureSwitchedConfigHandler>()!.DidReceive());
        }
        
        [Fact]
        public async Task When_Sending_A_Async_Command_To_The_Processor_When_A_Feature_Switch_Is_On_By_Fluent_Config()
        {
            await _commandProcessor.SendAsync(_myAsyncCommand);

            Assert.True(_provider.GetService<MyFeatureSwitchedConfigHandlerAsync>()!.DidReceive());
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
