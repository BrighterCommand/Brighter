using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class PipelineGlobalInboxWhenUseInboxTests
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private Pipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;
        private readonly InboxConfiguration _inboxConfiguration;
        private IAmAnInbox _inbox;


        public PipelineGlobalInboxWhenUseInboxTests()
        {
            _inbox = new InMemoryInbox();
            
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandInboxedHandler>();
            
            var container = new ServiceCollection();
            container.AddTransient<MyCommandInboxedHandler>();
            container.AddSingleton<IAmAnInbox>(_inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            
            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration(
                scope: InboxScope.All, 
                onceOnly: true, 
                actionOnExists: OnceOnlyAction.Throw);

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactory)handlerFactory, _inboxConfiguration);
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_Inbox()
        {
            // Settings for UseInbox on MyCommandInboxedHandler
            // [UseInbox(step:0, contextKey: typeof(MyCommandInboxedHandler), onceOnly: false)]
            // Settings for InboxConfifguration as above
            // _inboxConfiguration = new InboxConfiguration(InboxScope.All, context: true, onceOnly: true);
            // so global will not allow repeated requests ans calls, but local should override this and allow

            
            //act
            _chainOfResponsibility = _chainBuilder.Build(_requestContext);

            var chain = _chainOfResponsibility.First();
            var myCommand = new MyCommand();
            
            //First pass not impacted by UseInbox Handler
            chain.Handle(myCommand);

            bool noException = true;
            try
            {
                chain.Handle(myCommand);
            }
            catch (OnceOnlyException)
            {
                noException = false;
            }

            //assert
            noException.Should().BeTrue();

        }
        
        private PipelineTracer TracePipeline(IHandleRequests<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
 
    }
}
