#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageMappers
{
    public class MessageMapperRegistryDualInterfaceReleaseTests
    {
        [Fact]
        public void When_releasing_a_dual_interface_mapper_via_the_concrete_registry_it_is_not_ambiguous()
        {
            //arrange
            var syncFactory = new RecordingMapperFactory();
            var registry = new MessageMapperRegistry(syncFactory, new RecordingMapperFactoryAsync());

            //a mapper that supports both Reactor and Proactor - the common case, e.g. JsonMessageMapper<T>
            var mapper = new DualMapper();

            //act — a caller holding the concrete MessageMapperRegistry (public, widely held concretely)
            //releases a mapper that implements both marker interfaces. Before the fix this did not compile:
            //Release(IAmAMessageMapper) and Release(IAmAMessageMapperAsync) were both public and neither is
            //a better overload for a dual-interface argument (CS0121).
            registry.Release(mapper);

            //assert — the call binds to the synchronous Release, routing the mapper to the sync factory
            Assert.Same(mapper, syncFactory.Released);
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
            public IAmAMessageMapperAsync? Create(Type messageMapperType) => null;

            public void Release(IAmAMessageMapperAsync mapper) { }

            public ValueTask ReleaseAsync(IAmAMessageMapperAsync mapper) => default;
        }
    }
}
