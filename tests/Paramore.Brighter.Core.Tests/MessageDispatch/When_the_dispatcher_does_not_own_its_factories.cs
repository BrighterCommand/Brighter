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
    /// Regression for PR #4254 review finding 4. The Dispatcher became <see cref="IDisposable"/> in this change
    /// and disposes the mapper registry and transform factories it was handed. On the DI path it is the sole
    /// owner of a freshly-built graph, so disposal is correct. But on the manual-wiring path
    /// (<c>DispatchBuilder.MessageMappers</c>) the same registry is routinely shared with a
    /// <c>CommandProcessor</c>'s external bus, and unconditional disposal takes it out from under the other
    /// owner — every subsequent resolution throws <see cref="ObjectDisposedException"/>. Ownership must be
    /// declared, not assumed: unless it is told it owns them, the Dispatcher must not dispose a registry or
    /// factories it does not own. The DI/builder owning paths opt in; the default is safe for shared wiring.
    /// </summary>
    public class DispatcherOwnershipTests
    {
        [Fact]
        public void When_the_dispatcher_does_not_own_its_factories_it_does_not_dispose_them()
        {
            //arrange — the manual-wiring default: the Dispatcher shares the registry/factories with another owner
            var syncMapperFactory = new DisposeCountingMapperFactory();
            var asyncMapperFactory = new DisposeCountingMapperFactoryAsync();
            var mapperRegistry = new MessageMapperRegistry(syncMapperFactory, asyncMapperFactory);
            var syncTransformerFactory = new DisposeCountingTransformerFactory();
            var asyncTransformerFactory = new DisposeCountingTransformerFactoryAsync();

            var dispatcher = new Dispatcher(
                new SpyCommandProcessor(),
                new List<Subscription>(),
                Initializer.TestLoggerFactory, mapperRegistry,
                mapperRegistry,
                syncTransformerFactory,
                asyncTransformerFactory);

            //act
            dispatcher.Dispose();

            //assert — nothing the Dispatcher does not own is disposed, so the shared graph stays usable for the
            //other owner
            Assert.Equal(0, syncMapperFactory.DisposeCount);
            Assert.Equal(0, asyncMapperFactory.DisposeCount);
            Assert.Equal(0, syncTransformerFactory.DisposeCount);
            Assert.Equal(0, asyncTransformerFactory.DisposeCount);
        }

        [Fact]
        public void When_the_dispatcher_owns_only_the_registry_it_disposes_only_the_registry()
        {
            //arrange — the two ownership flags are independent: own the (shared-elsewhere) transform factories'
            //counterpart registry only
            var syncMapperFactory = new DisposeCountingMapperFactory();
            var asyncMapperFactory = new DisposeCountingMapperFactoryAsync();
            var mapperRegistry = new MessageMapperRegistry(syncMapperFactory, asyncMapperFactory);
            var syncTransformerFactory = new DisposeCountingTransformerFactory();
            var asyncTransformerFactory = new DisposeCountingTransformerFactoryAsync();

            var dispatcher = new Dispatcher(
                new SpyCommandProcessor(),
                new List<Subscription>(),
                Initializer.TestLoggerFactory, mapperRegistry,
                mapperRegistry,
                syncTransformerFactory,
                asyncTransformerFactory,
                ownsRegistry: true,
                ownsTransformerFactories: false);

            //act
            dispatcher.Dispose();

            //assert — the registry cascade disposes both mapper factories; the transform factories are left alone
            Assert.Equal(1, syncMapperFactory.DisposeCount);
            Assert.Equal(1, asyncMapperFactory.DisposeCount);
            Assert.Equal(0, syncTransformerFactory.DisposeCount);
            Assert.Equal(0, asyncTransformerFactory.DisposeCount);
        }

        [Fact]
        public void When_the_dispatcher_owns_only_the_transform_factories_it_disposes_only_them()
        {
            //arrange
            var syncMapperFactory = new DisposeCountingMapperFactory();
            var asyncMapperFactory = new DisposeCountingMapperFactoryAsync();
            var mapperRegistry = new MessageMapperRegistry(syncMapperFactory, asyncMapperFactory);
            var syncTransformerFactory = new DisposeCountingTransformerFactory();
            var asyncTransformerFactory = new DisposeCountingTransformerFactoryAsync();

            var dispatcher = new Dispatcher(
                new SpyCommandProcessor(),
                new List<Subscription>(),
                Initializer.TestLoggerFactory, mapperRegistry,
                mapperRegistry,
                syncTransformerFactory,
                asyncTransformerFactory,
                ownsRegistry: false,
                ownsTransformerFactories: true);

            //act
            dispatcher.Dispose();

            //assert — the transform factories are disposed; the shared registry (and its mapper factories) left alone
            Assert.Equal(0, syncMapperFactory.DisposeCount);
            Assert.Equal(0, asyncMapperFactory.DisposeCount);
            Assert.Equal(1, syncTransformerFactory.DisposeCount);
            Assert.Equal(1, asyncTransformerFactory.DisposeCount);
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
