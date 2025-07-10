using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Inbox.Handlers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineGlobalInboxWhenUseInboxTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private Pipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;


        public PipelineGlobalInboxWhenUseInboxTests()
        {
            IAmAnInboxSync inbox = new InMemoryInbox(new FakeTimeProvider());
            
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandInboxedHandler>();
            
            var container = new ServiceCollection();
            container.AddTransient<MyCommandInboxedHandler>();
            container.AddSingleton(inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            
            _requestContext = new RequestContext();
            
            InboxConfiguration inboxConfiguration = new(
                scope: InboxScope.All, 
                onceOnly: true, 
                actionOnExists: OnceOnlyAction.Throw);

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactorySync)handlerFactory, inboxConfiguration);
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_Inbox()
        {
            // Settings for InboxConfiguration on MyCommandInboxedHandler
            // [InboxConfiguration(step:0, contextKey: typeof(MyCommandInboxedHandler), onceOnly: false)]
            // Settings for InboxConfiguration as above
            // _inboxConfiguration = new InboxConfiguration(InboxScope.All, context: true, onceOnly: true);
            // so global will not allow repeated requests ans calls, but local should override this and allow

            
            //act
            _chainOfResponsibility = _chainBuilder.Build(new MyCommand(), _requestContext);

            var chain = _chainOfResponsibility.First();
            var myCommand = new MyCommand();
            
            //First pass not impacted by InboxConfiguration Handler
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
            Assert.True(noException);

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
