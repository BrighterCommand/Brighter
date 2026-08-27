using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    /// <summary>
    /// Registration is a set, not a list: adding the same (request type, handler type) pair twice
    /// must leave one handler. Two registration mechanisms can legitimately both cover a handler —
    /// <c>AutoFromAssemblies()</c> alongside a source-generated registration method, or a generated
    /// method called from two composition paths — and the caller means "register the union", not
    /// "register it twice and fail at dispatch".
    /// </summary>
    public class DuplicateHandlerRegistrationTests
    {
        [Fact]
        public void When_The_Same_Handler_Is_Registered_Twice_Only_One_Is_In_The_Pipeline()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            registry.Register<MyCommand, MyCommandHandler>();

            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler(new Dictionary<string, string>()));
            var pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
            PipelineBuilder<MyCommand>.ClearPipelineCache();

            var pipelines = pipelineBuilder.Build(new MyCommand(), new RequestContext());

            Assert.Single(pipelines);
        }

        [Fact]
        public void When_The_Same_Handler_Is_Registered_Twice_It_Is_Reported_Once()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            registry.Register<MyCommand, MyCommandHandler>();

            Assert.Single(registry.GetHandlerTypes(typeof(MyCommand)));
        }

        [Fact]
        public void When_Two_Different_Handlers_Are_Registered_Both_Are_Kept()
        {
            // Only exact duplicates collapse. Two genuinely different handlers for one command must
            // still reach the dispatch-time "more than one handler" check.
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            registry.Register<MyCommand, MyObsoleteCommandHandler>();

            Assert.Equal(2, registry.GetHandlerTypes(typeof(MyCommand)).Count);
        }
    }
}
