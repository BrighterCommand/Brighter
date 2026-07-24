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
    public void When_a_transform_release_throws_the_scope_still_releases_the_rest_without_double_releasing()
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

        //act — the first release surfaces the throw (an explicit Dispose must not swallow it); a retry then
        //runs, as the finalizer would after Dispose threw before GC.SuppressFinalize
        Assert.Throws<InvalidOperationException>(() => scope.Dispose());
        scope.Dispose();

        //assert — every transform is released exactly once across the two passes: none skipped, none twice.
        //Before the fix the retry re-ran the unmodified list, so 'before' was released twice and 'after'
        //never at all.
        Assert.Equal(1, factory.ReleaseCount(before));
        Assert.Equal(1, factory.ReleaseCount(throwing));
        Assert.Equal(1, factory.ReleaseCount(after));
    }

    [Fact]
    public async Task When_an_async_transform_release_throws_the_scope_still_releases_the_rest_without_double_releasing()
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

        //act — DisposeAsync surfaces the throw; a retry drains the rest
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await scope.DisposeAsync());
        await scope.DisposeAsync();

        //assert
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

        public IAmAMessageTransform? Create(Type transformerType) => null;

        public void Release(IAmAMessageTransform transformer)
        {
            _counts[transformer] = ReleaseCount(transformer) + 1;
            if (ReferenceEquals(transformer, _throwFor))
                throw new InvalidOperationException("release failed");
        }
    }

    private sealed class CountingReleaseFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        private readonly Dictionary<IAmAMessageTransformAsync, int> _counts = new();
        private IAmAMessageTransformAsync? _throwFor;

        public void ThrowFor(IAmAMessageTransformAsync transform) => _throwFor = transform;
        public int ReleaseCount(IAmAMessageTransformAsync transform) => _counts.TryGetValue(transform, out var c) ? c : 0;

        public IAmAMessageTransformAsync? Create(Type transformerType) => null;

        public void Release(IAmAMessageTransformAsync transformer)
        {
            _counts[transformer] = ReleaseCount(transformer) + 1;
            if (ReferenceEquals(transformer, _throwFor))
                throw new InvalidOperationException("release failed");
        }

        public ValueTask ReleaseAsync(IAmAMessageTransformAsync transformer)
        {
            Release(transformer);
            return default;
        }
    }
}
