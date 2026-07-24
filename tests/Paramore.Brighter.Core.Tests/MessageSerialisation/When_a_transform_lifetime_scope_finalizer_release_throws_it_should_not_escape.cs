using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

public class TransformLifetimeScopeFinalizerReleaseTests
{
    [Fact]
    public void When_a_transform_lifetime_scope_finalizer_release_throws_it_should_not_escape()
    {
        //arrange: a lifetime scope tracking a transform whose factory throws on release — the same shape
        //as MS DI's synchronous scope Dispose throwing for an IAsyncDisposable-only transform. The scope
        //is never disposed, so only the finalizer releases it. Finalization order is non-deterministic,
        //so an orphaned lifetime scope can be finalized before its owning pipeline disposes it; before the
        //finalizer guard this exception escaped ~TransformLifetimeScope and terminated the process.
        //Revert the try/catch in ~TransformLifetimeScope to prove RED: the test host crashes.
        CreateAndAbandonLifetimeScope();

        //act
        CollectAndRunFinalizers();

        //assert: reaching here means the finalizer swallowed the release exception instead of crashing
        Assert.True(true);
    }

    [Fact]
    public void When_an_async_transform_lifetime_scope_finalizer_release_throws_it_should_not_escape()
    {
        //arrange: the async lifetime scope's finalizer runs the same synchronous release path and carries
        //the same guard. Revert the try/catch in ~TransformLifetimeScopeAsync to prove RED.
        CreateAndAbandonLifetimeScopeAsync();

        //act
        CollectAndRunFinalizers();

        //assert
        Assert.True(true);
    }

    private static void CollectAndRunFinalizers()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    //isolated and non-inlined so the scope is not kept alive on the test method's stack frame and can be
    //collected on the following GC
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndAbandonLifetimeScope()
    {
        var scope = new TransformLifetimeScope(new ThrowingOnReleaseTransformerFactory());
        scope.Add(new MinimalTransform());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndAbandonLifetimeScopeAsync()
    {
        var scope = new TransformLifetimeScopeAsync(new ThrowingOnReleaseTransformerFactoryAsync());
        scope.Add(new MinimalTransformAsync());
    }

    private sealed class MinimalTransform : IAmAMessageTransform
    {
        public IRequestContext? Context { get; set; }
        public void Dispose() { }
        public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }
        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }
        public Message Wrap(Message message, Publication publication) => throw new NotImplementedException();
        public Message Unwrap(Message message) => throw new NotImplementedException();
    }

    private sealed class MinimalTransformAsync : IAmAMessageTransformAsync
    {
        public IRequestContext? Context { get; set; }
        public void Dispose() { }
        public void InitializeWrapFromAttributeParams(params object?[] initializerList) { }
        public void InitializeUnwrapFromAttributeParams(params object?[] initializerList) { }

        public Task<Message> WrapAsync(Message message, Publication publication,
            CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    //stands in for a factory-backed transformer factory whose synchronous release throws — e.g. releasing
    //a scope that holds an IAsyncDisposable-only transform through MS DI's throwing sync Dispose
    private sealed class ThrowingOnReleaseTransformerFactory : IAmAMessageTransformerFactory
    {
        public IAmAMessageTransform? Create(Type transformerType) => null;

        public void Release(IAmAMessageTransform transformer) =>
            throw new InvalidOperationException("release failed");
    }

    private sealed class ThrowingOnReleaseTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        public IAmAMessageTransformAsync? Create(Type transformerType) => null;

        public void Release(IAmAMessageTransformAsync transformer) =>
            throw new InvalidOperationException("release failed");

        public ValueTask ReleaseAsync(IAmAMessageTransformAsync transformer) =>
            throw new InvalidOperationException("release failed");
    }
}
