using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
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
            
            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);

            container.Register<IHandleRequests<MyCommand>, MyCommandInboxedHandler>();
            container.Register<IAmAnInbox>(_inbox);
 
            _requestContext = new RequestContext();
            
            _inboxConfiguration = new InboxConfiguration(
                scope: InboxScope.All, 
                useAutoContext: true, 
                onceOnly: true, 
                actionOnExists: OnceOnlyAction.Throw);

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory, _inboxConfiguration);
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_inbox()
        {
            // Settings for UseInbox on MyCommandInboxedHandler
            // [UseInbox(step:0, contextKey: typeof(MyCommandInboxedHandler), onceOnly: false)]
            // Settings for InboxConfifguration as above
            // _inboxConfiguration = new InboxConfiguration(InboxScope.All, useAutoContext: true, onceOnly: true);
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
