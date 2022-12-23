﻿using System;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class BuildPipelineFaultsAsync
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private readonly RequestContext _requestContext;

        public BuildPipelineFaultsAsync()
        {
               var registry = new SubscriberRegistry();
               registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
               
               //We'll simulate an IoC error
                IAmAHandlerFactoryAsync handlerFactory = new TestHandlerFactoryAsync<MyCommand, MyCommandHandlerAsync>(() => throw new InvalidOperationException("Could no create handler"));
               _requestContext = new RequestContext();
   
               _chainBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
               PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_Building_A_Pipeline_Failures_Should_Be_ConfigurationErrors()
        {
            var exception = Catch.Exception(() => _chainBuilder.BuildAsync(_requestContext, false));
            exception.Should().NotBeNull();
            exception.Should().BeOfType<ConfigurationException>();
            exception.InnerException.Should().BeOfType<InvalidOperationException>();
        }
    }
}
