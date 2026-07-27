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
        //implements both marker interfaces. Both Release overloads are explicit interface implementations,
        //so a caller holding the concrete MessageMapperRegistry cannot call Release(dualMapper) directly:
        //it must pick IAmAMessageMapperRegistry or IAmAMessageMapperRegistryAsync. That turns "released a
        //mapper resolved from GetAsync through the sync factory" from a silent leak into a compile error,
        //and each interface routes to its own factory - the two facts below.

        [Fact]
        public void When_releasing_a_dual_interface_mapper_via_the_sync_registry_it_routes_to_the_sync_factory()
        {
            //arrange
            var syncFactory = new RecordingMapperFactory();
            var asyncFactory = new RecordingMapperFactoryAsync();
            var registry = new MessageMapperRegistry(syncFactory, asyncFactory);
            var mapper = new DualMapper();

            //act — release through the synchronous registry interface
            ((IAmAMessageMapperRegistry)registry).Release(mapper);

            //assert — routed to the sync factory, not the async one
            Assert.Same(mapper, syncFactory.Released);
            Assert.Null(asyncFactory.Released);
        }

        [Fact]
        public void When_releasing_a_dual_interface_mapper_via_the_async_registry_it_routes_to_the_async_factory()
        {
            //arrange
            var syncFactory = new RecordingMapperFactory();
            var asyncFactory = new RecordingMapperFactoryAsync();
            var registry = new MessageMapperRegistry(syncFactory, asyncFactory);
            var mapper = new DualMapper();

            //act — a mapper resolved from GetAsync must be released through the async registry interface so
            //it reaches the factory that created it
            ((IAmAMessageMapperRegistryAsync)registry).Release(mapper);

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

            public IAmAMessageMapper? Create(Type messageMapperType) => null;

            public void Release(IAmAMessageMapper mapper) => Released = mapper;
        }

        private sealed class RecordingMapperFactoryAsync : IAmAMessageMapperFactoryAsync
        {
            public IAmAMessageMapperAsync? Released { get; private set; }

            public IAmAMessageMapperAsync? Create(Type messageMapperType) => null;

            public void Release(IAmAMessageMapperAsync mapper) => Released = mapper;

            public ValueTask ReleaseAsync(IAmAMessageMapperAsync mapper) => default;
        }
    }
}
