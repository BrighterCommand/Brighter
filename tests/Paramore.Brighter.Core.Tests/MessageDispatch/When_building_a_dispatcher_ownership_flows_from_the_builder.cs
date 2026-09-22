#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    /// <summary>
    /// Regression for PR #4254 review finding 4. Ownership must flow from the construction site through
    /// <see cref="DispatchBuilder"/> to the Dispatcher: the manual fluent-build path defaults to not owning the
    /// shared registry/factories, while the DI path (<c>BuildDispatcher</c>) passes <c>Build(true, true)</c>
    /// because it news up a graph solely for the Dispatcher. These pin both ends of that thread so the DI path
    /// cannot silently stop disposing the graph (regressing the shutdown-retention fix) and the manual path
    /// cannot silently dispose a shared registry.
    /// </summary>
    public class DispatchBuilderOwnershipTests
    {
        [Fact]
        public void When_building_without_declaring_ownership_the_dispatcher_does_not_dispose_the_graph()
        {
            var syncMapperFactory = new DisposeCountingMapperFactory();
            var asyncMapperFactory = new DisposeCountingMapperFactoryAsync();
            var mapperRegistry = new MessageMapperRegistry(syncMapperFactory, asyncMapperFactory);
            var syncTransformerFactory = new DisposeCountingTransformerFactory();
            var asyncTransformerFactory = new DisposeCountingTransformerFactoryAsync();

            var dispatcher = BuildDispatcher(mapperRegistry, syncTransformerFactory, asyncTransformerFactory)
                .Build();

            dispatcher.Dispose();

            Assert.Equal(0, syncMapperFactory.DisposeCount);
            Assert.Equal(0, asyncMapperFactory.DisposeCount);
            Assert.Equal(0, syncTransformerFactory.DisposeCount);
            Assert.Equal(0, asyncTransformerFactory.DisposeCount);
        }

        [Fact]
        public void When_building_and_declaring_ownership_the_dispatcher_disposes_the_graph()
        {
            var syncMapperFactory = new DisposeCountingMapperFactory();
            var asyncMapperFactory = new DisposeCountingMapperFactoryAsync();
            var mapperRegistry = new MessageMapperRegistry(syncMapperFactory, asyncMapperFactory);
            var syncTransformerFactory = new DisposeCountingTransformerFactory();
            var asyncTransformerFactory = new DisposeCountingTransformerFactoryAsync();

            var dispatcher = BuildDispatcher(mapperRegistry, syncTransformerFactory, asyncTransformerFactory)
                .Build(ownsRegistry: true, ownsTransformerFactories: true);

            dispatcher.Dispose();

            Assert.Equal(1, syncMapperFactory.DisposeCount);
            Assert.Equal(1, asyncMapperFactory.DisposeCount);
            Assert.Equal(1, syncTransformerFactory.DisposeCount);
            Assert.Equal(1, asyncTransformerFactory.DisposeCount);
        }

        private static IAmADispatchBuilder BuildDispatcher(
            MessageMapperRegistry mapperRegistry,
            IAmAMessageTransformerFactory syncTransformerFactory,
            IAmAMessageTransformerFactoryAsync asyncTransformerFactory)
        {
            var channelFactory = new InMemoryChannelFactory(new InternalBus(), TimeProvider.System);
            return DispatchBuilder
                .StartNew()
                .CommandProcessor(new SpyCommandProcessor(), new InMemoryRequestContextFactory())
                .MessageMappers(mapperRegistry, mapperRegistry, syncTransformerFactory, asyncTransformerFactory)
                .ChannelFactory(channelFactory)
                .Subscriptions(new List<Subscription>())
                .NoInstrumentation();
        }

        private sealed class DisposeCountingMapperFactory : IAmAMessageMapperFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public Lease<IAmAMessageMapper>? Create(Type messageMapperType) => null;
            public void Release(Lease<IAmAMessageMapper>? lease) { }
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingMapperFactoryAsync : IAmAMessageMapperFactoryAsync, IDisposable
        {
            public int DisposeCount { get; private set; }
            public Lease<IAmAMessageMapperAsync>? Create(Type messageMapperType) => null;
            public void Release(Lease<IAmAMessageMapperAsync>? lease) { }
            public ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync>? lease) => default;
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingTransformerFactory : IAmAMessageTransformerFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public Lease<IAmAMessageTransform>? Create(Type transformerType) => null;
            public void Release(Lease<IAmAMessageTransform>? lease) { }
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync, IDisposable
        {
            public int DisposeCount { get; private set; }
            public Lease<IAmAMessageTransformAsync>? Create(Type transformerType) => null;
            public void Release(Lease<IAmAMessageTransformAsync>? lease) { }
            public ValueTask ReleaseAsync(Lease<IAmAMessageTransformAsync>? lease) => default;
            public void Dispose() => DisposeCount++;
        }
    }
}
