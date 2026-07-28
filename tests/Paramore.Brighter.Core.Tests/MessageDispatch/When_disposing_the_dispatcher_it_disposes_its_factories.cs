#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class DispatcherDisposalTests
    {
        [Fact]
        public void When_disposing_the_dispatcher_it_disposes_the_registry_and_transform_factories()
        {
            //arrange
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var syncMapperFactory = new DisposeCountingMapperFactory();
            var asyncMapperFactory = new DisposeCountingMapperFactoryAsync();

            //the DI path (BuildDispatcher) news one MessageMapperRegistry and passes it as both the sync and
            //the async registry, so use the same instance here to mirror production
            var mapperRegistry = new MessageMapperRegistry(syncMapperFactory, asyncMapperFactory);

            var syncTransformerFactory = new DisposeCountingTransformerFactory();
            var asyncTransformerFactory = new DisposeCountingTransformerFactoryAsync();

            var dispatcher = new Dispatcher(
                commandProcessor,
                new List<Subscription>(),
                mapperRegistry,
                mapperRegistry,
                syncTransformerFactory,
                asyncTransformerFactory);

            //act — the Dispatcher is the sole owner of the runtime mapper/transform graph built for it in
            //BuildDispatcher. The container disposes the Dispatcher (a singleton) at shutdown; the Dispatcher
            //must cascade to the factories that hold per-resolution scopes, otherwise those scopes are
            //retained until the process exits (the consumer-side half of the #4252 retention).
            dispatcher.Dispose();

            //assert — the registry cascade disposes both mapper factories; both transform factories directly
            Assert.Equal(1, syncMapperFactory.DisposeCount);
            Assert.Equal(1, asyncMapperFactory.DisposeCount);
            Assert.Equal(1, syncTransformerFactory.DisposeCount);
            Assert.Equal(1, asyncTransformerFactory.DisposeCount);
        }

        [Fact]
        public void When_disposing_the_dispatcher_twice_it_disposes_each_factory_once()
        {
            //arrange
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var syncMapperFactory = new DisposeCountingMapperFactory();
            var asyncMapperFactory = new DisposeCountingMapperFactoryAsync();
            var mapperRegistry = new MessageMapperRegistry(syncMapperFactory, asyncMapperFactory);

            var syncTransformerFactory = new DisposeCountingTransformerFactory();
            var asyncTransformerFactory = new DisposeCountingTransformerFactoryAsync();

            var dispatcher = new Dispatcher(
                commandProcessor,
                new List<Subscription>(),
                mapperRegistry,
                mapperRegistry,
                syncTransformerFactory,
                asyncTransformerFactory);

            //act — a second Dispose() (an application-level one after the container's) must be claimed and
            //must not re-run the factory disposals
            dispatcher.Dispose();
            dispatcher.Dispose();

            //assert — every factory disposed exactly once despite the double dispose
            Assert.Equal(1, syncMapperFactory.DisposeCount);
            Assert.Equal(1, asyncMapperFactory.DisposeCount);
            Assert.Equal(1, syncTransformerFactory.DisposeCount);
            Assert.Equal(1, asyncTransformerFactory.DisposeCount);
        }

        private sealed class DisposeCountingMapperFactory : IAmAMessageMapperFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public IAmAMessageMapper? Create(Type messageMapperType) => null;
            public void Release(IAmAMessageMapper mapper) { }
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingMapperFactoryAsync : IAmAMessageMapperFactoryAsync, IDisposable
        {
            public int DisposeCount { get; private set; }
            public IAmAMessageMapperAsync? Create(Type messageMapperType) => null;
            public void Release(IAmAMessageMapperAsync mapper) { }
            public ValueTask ReleaseAsync(IAmAMessageMapperAsync mapper) => default;
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingTransformerFactory : IAmAMessageTransformerFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public IAmAMessageTransform? Create(Type transformerType) => null;
            public void Release(IAmAMessageTransform transformer) { }
            public void Dispose() => DisposeCount++;
        }

        private sealed class DisposeCountingTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync, IDisposable
        {
            public int DisposeCount { get; private set; }
            public IAmAMessageTransformAsync? Create(Type transformerType) => null;
            public void Release(IAmAMessageTransformAsync transformer) { }
            public ValueTask ReleaseAsync(IAmAMessageTransformAsync transformer) => default;
            public void Dispose() => DisposeCount++;
        }
    }
}
