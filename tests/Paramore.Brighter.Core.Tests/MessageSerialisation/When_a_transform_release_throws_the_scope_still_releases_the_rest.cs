#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class TransformLifetimeScopePartialReleaseTests
{
    [Fact]
    public void When_a_transform_release_throws_the_scope_drains_the_rest_and_surfaces_an_aggregate()
    {
        //arrange — three tracked transforms; the factory throws when releasing the middle one, the same
        //shape as MS DI's synchronous scope Dispose throwing for an IAsyncDisposable-only transform
        var factory = new CountingReleaseFactory();
        var before = new CountingTransform();
        var throwing = new CountingTransform();
        var after = new CountingTransform();
        factory.ThrowFor(throwing);

        var scope = new TransformLifetimeScope(factory);
        scope.Add(before);
        scope.Add(throwing);
        scope.Add(after);

        //act — a single explicit Dispose drains every transform deterministically (it does not abort on the
        //throwing one and leave the rest to the GC-timed finalizer) and surfaces the failure as an
        //AggregateException rather than swallowing it
        var aggregate = Assert.Throws<AggregateException>(() => scope.Dispose());

        //assert — the drain completed in that one pass: every transform released exactly once, none skipped;
        //and the AggregateException carries the original release failure
        Assert.Equal(1, factory.ReleaseCount(before));
        Assert.Equal(1, factory.ReleaseCount(throwing));
        Assert.Equal(1, factory.ReleaseCount(after));
        Assert.IsType<InvalidOperationException>(Assert.Single(aggregate.InnerExceptions));

        //a second Dispose finds an empty list (the first drained it) — no re-release
        scope.Dispose();
        Assert.Equal(1, factory.ReleaseCount(before));
        Assert.Equal(1, factory.ReleaseCount(throwing));
        Assert.Equal(1, factory.ReleaseCount(after));
    }

    [Fact]
    public async Task When_an_async_transform_release_throws_the_scope_drains_the_rest_and_surfaces_an_aggregate()
    {
        //arrange
        var factory = new CountingReleaseFactoryAsync();
        var before = new CountingTransformAsync();
        var throwing = new CountingTransformAsync();
        var after = new CountingTransformAsync();
        factory.ThrowFor(throwing);

        var scope = new TransformLifetimeScopeAsync(factory);
        scope.Add(before);
        scope.Add(throwing);
        scope.Add(after);

        //act — a single DisposeAsync drains the rest deterministically and surfaces the failure as an aggregate
        var aggregate = await Assert.ThrowsAsync<AggregateException>(async () => await scope.DisposeAsync());

        //assert
        Assert.Equal(1, factory.ReleaseCount(before));
        Assert.Equal(1, factory.ReleaseCount(throwing));
        Assert.Equal(1, factory.ReleaseCount(after));
        Assert.IsType<InvalidOperationException>(Assert.Single(aggregate.InnerExceptions));

        await scope.DisposeAsync();
        Assert.Equal(1, factory.ReleaseCount(before));
        Assert.Equal(1, factory.ReleaseCount(throwing));
        Assert.Equal(1, factory.ReleaseCount(after));
    }

    private sealed class CountingTransform : IAmAMessageTransform
    {
        public IRequestContext? Context { get; set; }
        public void Dispose() { }
        public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }
        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }
        public Message Wrap(Message message, Publication publication) => throw new NotImplementedException();
        public Message Unwrap(Message message) => throw new NotImplementedException();
    }

    private sealed class CountingTransformAsync : IAmAMessageTransformAsync
    {
        public IRequestContext? Context { get; set; }
        public void Dispose() { }
        public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }
        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }
        public Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class CountingReleaseFactory : IAmAMessageTransformerFactory
    {
        private readonly Dictionary<IAmAMessageTransform, int> _counts = new();
        private IAmAMessageTransform? _throwFor;

        public void ThrowFor(IAmAMessageTransform transform) => _throwFor = transform;
        public int ReleaseCount(IAmAMessageTransform transform) => _counts.TryGetValue(transform, out var c) ? c : 0;

        public Lease<IAmAMessageTransform>? Create(Type transformerType) => null;

        public void Release(Lease<IAmAMessageTransform> lease)
        {
            _counts[lease.Instance] = ReleaseCount(lease.Instance) + 1;
            if (ReferenceEquals(lease.Instance, _throwFor))
                throw new InvalidOperationException("release failed");
        }
    }

    private sealed class CountingReleaseFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        private readonly Dictionary<IAmAMessageTransformAsync, int> _counts = new();
        private IAmAMessageTransformAsync? _throwFor;

        public void ThrowFor(IAmAMessageTransformAsync transform) => _throwFor = transform;
        public int ReleaseCount(IAmAMessageTransformAsync transform) => _counts.TryGetValue(transform, out var c) ? c : 0;

        public Lease<IAmAMessageTransformAsync>? Create(Type transformerType) => null;

        public void Release(Lease<IAmAMessageTransformAsync> lease)
        {
            _counts[lease.Instance] = ReleaseCount(lease.Instance) + 1;
            if (ReferenceEquals(lease.Instance, _throwFor))
                throw new InvalidOperationException("release failed");
        }

        public ValueTask ReleaseAsync(Lease<IAmAMessageTransformAsync> lease)
        {
            Release(lease);
            return default;
        }
    }
}
