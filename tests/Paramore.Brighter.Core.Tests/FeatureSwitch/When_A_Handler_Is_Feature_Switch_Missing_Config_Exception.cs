using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.FeatureSwitch.TestDoubles;
using Paramore.Brighter.FeatureSwitch;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.FeatureSwitch.Handlers;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.FeatureSwitch
{
    [Collection("CommandProcessor")] 
    public class FeatureSwitchByConfigMissingConfigStrategyExceptionTests : IDisposable
    {
        private readonly MyCommand _myCommand = new();
        private readonly MyCommandAsync _myAsyncCommand = new();

        private readonly CommandProcessor _commandProcessor;
        private readonly ServiceProvider _provider;
        private Exception _exception;

        public FeatureSwitchByConfigMissingConfigStrategyExceptionTests()
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
            
            IAmAFeatureSwitchRegistry featureSwitchRegistry = new FakeConfigRegistry();
            
            featureSwitchRegistry.MissingConfigStrategy = MissingConfigStrategy.Exception;

            _commandProcessor = CommandProcessorBuilder
                .StartNew()
                .ConfigureFeatureSwitches(featureSwitchRegistry)
                .Handlers(new HandlerConfiguration(registry, handlerFactory))
                .DefaultResilience()
                .NoExternalBus()
                .ConfigureInstrumentation(new BrighterTracer(TimeProvider.System), InstrumentationOptions.All)
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .RequestSchedulerFactory(new InMemorySchedulerFactory())
                .Build();
        }        

        [Fact]
        public void When_Sending_A_Command_To_The_Processor_When_A_Feature_Switch_Has_No_Config_And_Strategy_Is_Exception()
        {
            _exception = Catch.Exception(() => _commandProcessor.Send(_myCommand));

            Assert.IsType<ConfigurationException>(_exception);
            Assert.NotNull(_exception);
            Assert.Contains($"Handler of type {nameof(MyFeatureSwitchedConfigHandler)} does not have a Feature Switch configuration!", _exception.Message);

            Assert.False(_provider.GetService<MyFeatureSwitchedConfigHandler>()!.DidReceive());

        }        

        [Fact]
        public async Task When_Sending_A_Async_Command_To_The_Processor_When_A_Feature_Switch_Has_No_Config_And_Strategy_Is_Exception()
        {
            try
            {
                await _commandProcessor.SendAsync(_myAsyncCommand);
            }
            catch (Exception e)
            {
                _exception = e;
            }
                

            Assert.IsType<ConfigurationException>(_exception);
            Assert.NotNull(_exception);
            Assert.Contains($"Handler of type {nameof(MyFeatureSwitchedConfigHandlerAsync)} does not have a Feature Switch configuration!", _exception.Message);

            Assert.False(_provider.GetService<MyFeatureSwitchedConfigHandlerAsync>()!.DidReceive());

        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
