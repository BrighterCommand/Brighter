using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class PipelineGlobalInboxNoInboxAttributeTests
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private Pipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;
        private readonly InboxConfiguration _inboxConfiguration;
        private IAmAnInbox _inbox;


        public PipelineGlobalInboxNoInboxAttributeTests()
        {
            _inbox = new InMemoryInbox();
            
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyNoInboxCommandHandler>();
            
            var container = new ServiceCollection();
            container.AddTransient<MyNoInboxCommandHandler>();
            container.AddSingleton<IAmAnInbox>(_inbox);

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactory)handlerFactory, _inboxConfiguration);
            
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
        
        private PipelineTracer TracePipeline(IHandleRequests<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
 
    }
}
