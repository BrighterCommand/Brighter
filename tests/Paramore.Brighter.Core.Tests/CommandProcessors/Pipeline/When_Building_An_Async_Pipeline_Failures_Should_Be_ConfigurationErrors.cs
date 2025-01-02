using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class BuildPipelineFaultsAsync
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private readonly RequestContext _requestContext;
        private SubscriberRegistry _subscriberRegistry;

        public BuildPipelineFaultsAsync()
        {
            _subscriberRegistry = new SubscriberRegistry();
            _subscriberRegistry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
               
               //We'll simulate an IoC error
                IAmAHandlerFactoryAsync handlerFactory = new SimpleHandlerFactoryAsync(_ => throw new InvalidOperationException("Could no create handler"));
               _requestContext = new RequestContext();
   
               _chainBuilder = new PipelineBuilder<MyCommand>(handlerFactory);
               PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_Building_A_Pipeline_Failures_Should_Be_ConfigurationErrors()
        {
            var observers = _subscriberRegistry.Get<MyCommand>();
            var exception = Catch.Exception(() => _chainBuilder.BuildAsync(observers.First(), _requestContext, false));
            exception.Should().NotBeNull();
            exception.Should().BeOfType<ConfigurationException>();
            exception.InnerException.Should().BeOfType<InvalidOperationException>();
        }
    }
}
