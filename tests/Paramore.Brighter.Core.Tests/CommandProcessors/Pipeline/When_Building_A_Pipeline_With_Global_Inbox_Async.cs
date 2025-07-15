using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    //TODO:
    //Respects different global choices i.e. throw, what to capture, context
    //allow a lambda for the context, to override, and pass in a default of typeof() ????
 
    [Collection("CommandProcessor")]
    public class PipelineGlobalInboxTestsAsync : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private AsyncPipelines<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;


        public PipelineGlobalInboxTestsAsync()
        {
            IAmAnInboxAsync inbox = new InMemoryInbox(new FakeTimeProvider());
            var handler = new MyCommandHandlerAsync(new Dictionary<string, string>());
           
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            
            var container = new ServiceCollection();
            container.AddSingleton(handler);
            container.AddSingleton(inbox);
            container.AddTransient<UseInboxHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());


            _requestContext = new RequestContext();
            
            InboxConfiguration inboxConfiguration = new();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory, inboxConfiguration);
            
        }

        [Fact]
        public void When_Building_A_Pipeline_With_Global_Inbox()
        {
           //act
            _chainOfResponsibility = _chainBuilder.BuildAsync(new MyCommand(), _requestContext, false);
            
            //assert
            var tracer = TracePipeline(_chainOfResponsibility.First());
            Assert.Contains("UseInboxHandlerAsync", tracer.ToString());

        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }

        private PipelineTracer TracePipeline(IHandleRequestsAsync<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
 
    }
}
