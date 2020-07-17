using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class PipelineGlobalInboxTests
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private Pipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;
        private readonly InboxConfiguration _inboxConfiguration;
        private IAmAnInbox _inbox;


        public PipelineGlobalInboxTests()
        {
            _inbox = new InMemoryInbox();
            
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            
            var container = new ServiceCollection();
            container.AddTransient<MyCommandHandler>();
            container.AddSingleton<IAmAnInbox>(_inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactory)handlerFactory, _inboxConfiguration);
            PipelineBuilder<MyCommand>.ClearPipelineCache(); 
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_inbox()
        {
            //act
            _chainOfResponsibility = _chainBuilder.Build(_requestContext);
            
            //assert
            var tracer = TracePipeline(_chainOfResponsibility.First());
            tracer.ToString().Should().Contain("UseInboxHandler");

        }
        
        private PipelineTracer TracePipeline(IHandleRequests<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
 
    }
}
