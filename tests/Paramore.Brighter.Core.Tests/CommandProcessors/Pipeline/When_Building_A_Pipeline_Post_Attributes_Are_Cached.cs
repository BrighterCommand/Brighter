using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class When_Building_A_Pipeline_Post_Attributes_Are_Cached
    {
        [Fact]
        public void When_Building_A_Sync_Pipeline_Post_Attributes_Are_Cached_For_The_Handler()
        {
            PipelineBuilder<MyCommand>.ClearPipelineCache();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyPreAndPostDecoratedHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyPreAndPostDecoratedHandler>();
            container.AddTransient<MyValidationHandler<MyCommand>>();
            container.AddTransient<MyLoggingHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            var pipelineBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactorySync)handlerFactory);

            pipelineBuilder.Build(new MyCommand(), new RequestContext()).First();

            Assert.Contains(nameof(MyPreAndPostDecoratedHandler), GetPostAttributesCacheKeys());
        }

        [Fact]
        public void When_Building_An_Async_Pipeline_Post_Attributes_Are_Cached_For_The_Handler()
        {
            PipelineBuilder<MyCommand>.ClearPipelineCache();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyPreAndPostDecoratedHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyPreAndPostDecoratedHandlerAsync>();
            container.AddTransient<MyValidationHandlerAsync<MyCommand>>();
            container.AddTransient<MyLoggingHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            var pipelineBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory);

            pipelineBuilder.BuildAsync(new MyCommand(), new RequestContext(), false).First();

            Assert.Contains(nameof(MyPreAndPostDecoratedHandlerAsync), GetPostAttributesCacheKeys());
        }

        private static IEnumerable<string> GetPostAttributesCacheKeys()
        {
            var field = typeof(PipelineBuilder<MyCommand>).GetField(
                "s_postAttributesMemento",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);

            var cache = (IDictionary)field!.GetValue(null)!;
            return cache.Keys.Cast<string>();
        }
    }
}
