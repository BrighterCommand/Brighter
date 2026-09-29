using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    // Tests mutate static PipelineBuilder<MyCommand> caches; serialise to avoid clobbering each other.
    [NotInParallel(nameof(When_Building_A_Pipeline_Post_Attributes_Are_Cached))]
    public class When_Building_A_Pipeline_Post_Attributes_Are_Cached
    {
        [Test]
        public async Task When_Building_A_Sync_Pipeline_Post_Attributes_Are_Cached_For_The_Handler()
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

            await Assert.That(await GetPostAttributesCacheKeys()).Contains(typeof(MyPreAndPostDecoratedHandler));
        }

        [Test]
        public async Task When_Building_An_Async_Pipeline_Post_Attributes_Are_Cached_For_The_Handler()
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

            await Assert.That(await GetPostAttributesCacheKeys()).Contains(typeof(MyPreAndPostDecoratedHandlerAsync));
        }

        private static async Task<IEnumerable<Type>> GetPostAttributesCacheKeys()
        {
            var field = typeof(PipelineBuilder<MyCommand>).GetField(
                "s_postAttributesMemento",
                BindingFlags.Static | BindingFlags.NonPublic);
            await Assert.That(field).IsNotNull();

            var cache = (IDictionary)field!.GetValue(null)!;
            return cache.Keys.Cast<Type>();
        }
    }
}
