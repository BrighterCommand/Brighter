using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineGlobalInboxTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private IEnumerable<IHandleRequests<MyCommand>> _chainOfResponsibility;
        private readonly RequestContext _requestContext;
        private SubscriberRegistry _subscriberRegistry;

        public PipelineGlobalInboxTests()
        {
            IAmAnInboxSync inbox = new InMemoryInbox(new FakeTimeProvider());
            
            _subscriberRegistry = new SubscriberRegistry();
            _subscriberRegistry.Register<MyCommand, MyCommandHandler>();
            
            var container = new ServiceCollection();
            container.AddTransient<MyCommandHandler>();
            container.AddSingleton(inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            _requestContext = new RequestContext();
            
            InboxConfiguration inboxConfiguration = new();

            _chainBuilder = new PipelineBuilder<MyCommand>((IAmAHandlerFactorySync)handlerFactory, inboxConfiguration);
            PipelineBuilder<MyCommand>.ClearPipelineCache(); 
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_inbox()
        {
            //act
            var observers = _subscriberRegistry.Get<MyCommand>();
            _chainOfResponsibility = observers.Select(o => _chainBuilder.Build(o, _requestContext));
            
            //assert
            var tracer = TracePipeline(_chainOfResponsibility.First());
            tracer.ToString().Should().Contain("UseInboxHandler");

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
