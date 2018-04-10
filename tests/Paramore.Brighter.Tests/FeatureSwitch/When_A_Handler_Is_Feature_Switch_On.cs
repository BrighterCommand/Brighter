using System;
using FluentAssertions;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.FeatureSwitch.TestDoubles;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.FeatureSwitch
{
    [Collection("Feature Switch Check")]
    public class CommandProcessorWithFeatureSwitchOnInPipelineTests : IDisposable
    {
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly SubscriberRegistry _registry;
        private readonly TinyIocHandlerFactory _handlerFactory;

        private CommandProcessor _commandProcessor;

        public CommandProcessorWithFeatureSwitchOnInPipelineTests()
        {
            _registry = new SubscriberRegistry();
            _registry.Register<MyCommand, MyFeatureSwitchedOnHandler>();

            var container = new TinyIoCContainer();
            _handlerFactory = new TinyIocHandlerFactory(container);

            container.Register<IHandleRequests<MyCommand>, MyFeatureSwitchedOnHandler>().AsSingleton();            
        }

        [Fact]
        public void When_Sending_A_Command_To_The_Processor_When_A_Feature_Switch_Is_On()
        {
            _commandProcessor = new CommandProcessor(_registry, _handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
            _commandProcessor.Send(_myCommand);

            MyFeatureSwitchedOnHandler.DidReceive(_myCommand).Should().BeTrue();
        }

        [Fact]
        public void When_Sending_A_Command_To_The_Processor_When_A_Feature_Switch_Is_On_By_Config()
        {
            _commandProcessor = new CommandProcessor(_registry, _handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
            _commandProcessor.Send(_myCommand);

            MyFeatureSwitchedOnHandler.DidReceive(_myCommand).Should().BeTrue();
        }

        public void Dispose()
        {
            _commandProcessor?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
