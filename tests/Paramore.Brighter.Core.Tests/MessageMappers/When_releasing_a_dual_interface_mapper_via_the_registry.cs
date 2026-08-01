#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageMappers
{
    public class MessageMapperRegistryDualInterfaceReleaseTests
    {
        //A mapper that supports both Reactor and Proactor - the common case, e.g. JsonMessageMapper<T> -
        //implements both marker interfaces. Since the redesign, Release keys on the lease, whose generic
        //argument carries the interface: Get<T> returns a Lease<IAmAMessageMapper<T>> and GetAsync<T> a
        //Lease<IAmAMessageMapperAsync<T>>, so the two Release<T> overloads are distinguished by lease type.
        //A mapper resolved from GetAsync is an async-typed lease that can ONLY bind to the async overload -
        //routing it to the sync factory is a compile-time type error, not a silent leak - and each overload
        //routes to its own factory, the two facts below.

        [Fact]
        public void When_releasing_a_dual_interface_mapper_via_a_sync_lease_it_routes_to_the_sync_factory()
        {
            //arrange
            var syncFactory = new RecordingMapperFactory();
            var asyncFactory = new RecordingMapperFactoryAsync();
            var registry = new MessageMapperRegistry(syncFactory, asyncFactory);
            var mapper = new DualMapper();
            //the lease's type is IAmAMessageMapper<DualRequest> — as Get<DualRequest> would return
            var syncLease = new Lease<IAmAMessageMapper<DualRequest>>(mapper);

            //act — the sync-typed lease binds to the sync Release overload
            registry.Release(syncLease);

            //assert — routed to the sync factory, not the async one
            Assert.Same(mapper, syncFactory.Released);
            Assert.Null(asyncFactory.Released);
        }

        [Fact]
        public void When_releasing_a_dual_interface_mapper_via_an_async_lease_it_routes_to_the_async_factory()
        {
            //arrange
            var syncFactory = new RecordingMapperFactory();
            var asyncFactory = new RecordingMapperFactoryAsync();
            var registry = new MessageMapperRegistry(syncFactory, asyncFactory);
            var mapper = new DualMapper();
            //a mapper resolved from GetAsync is an IAmAMessageMapperAsync<DualRequest>-typed lease; it can
            //only bind to the async Release overload, so it reaches the factory that created it
            var asyncLease = new Lease<IAmAMessageMapperAsync<DualRequest>>(mapper);

            //act
            registry.Release(asyncLease);

            //assert — routed to the async factory, not the sync one
            Assert.Same(mapper, asyncFactory.Released);
            Assert.Null(syncFactory.Released);
        }

        private sealed class DualRequest : Command
        {
            public DualRequest() : base(Guid.NewGuid()) { }
        }

        private sealed class DualMapper : IAmAMessageMapper<DualRequest>, IAmAMessageMapperAsync<DualRequest>
        {
            public IRequestContext? Context { get; set; }

            public Message MapToMessage(DualRequest request, Publication publication) => throw new NotImplementedException();

            public DualRequest MapToRequest(Message message) => throw new NotImplementedException();

            public Task<Message> MapToMessageAsync(DualRequest request, Publication publication, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<DualRequest> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
        }

        private sealed class RecordingMapperFactory : IAmAMessageMapperFactory
        {
            public IAmAMessageMapper? Released { get; private set; }

            public Lease<IAmAMessageMapper>? Create(Type messageMapperType) => null;

            public void Release(Lease<IAmAMessageMapper>? lease) => Released = lease!.Instance;
        }

        private sealed class RecordingMapperFactoryAsync : IAmAMessageMapperFactoryAsync
        {
            public IAmAMessageMapperAsync? Released { get; private set; }

            public Lease<IAmAMessageMapperAsync>? Create(Type messageMapperType) => null;

            public void Release(Lease<IAmAMessageMapperAsync>? lease) => Released = lease!.Instance;

            public ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync>? lease) => default;
        }
    }
}
