﻿using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    [Collection("CommandProcessor")]
    public class PipelineGlobalInboxNoInboxAttributeTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private Pipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;


        public PipelineGlobalInboxNoInboxAttributeTests()
        {
            IAmAnInboxSync inbox = new InMemoryInbox(new FakeTimeProvider());
            
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyNoInboxCommandHandler>();
            
            var container = new ServiceCollection();
            container.AddTransient<MyNoInboxCommandHandler>();
            container.AddSingleton<IAmAnInboxSync>(inbox);
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _requestContext = new RequestContext();
            
            InboxConfiguration inboxConfiguration = new();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactorySync)handlerFactory, inboxConfiguration);
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_Inbox()
        {
            //act
            _chainOfResponsibility = _chainBuilder.Build(_requestContext);
            
            //assert
            var tracer = TracePipeline(_chainOfResponsibility.First());
            tracer.ToString().Should().NotContain("UseInboxHandler");

        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
        
        private PipelineTracer TracePipeline(IHandleRequests<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
 
    }
}
