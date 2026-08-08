using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

[Collection(FinalizerTestCollection.Name)]
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

        //assert: the finalizer genuinely ran and attempted the release that throws (pinning that the GC
        //actually collected the abandoned scope, not a vacuous pass), and reaching here means the
        //exception was swallowed instead of escaping ~TransformLifetimeScope and crashing the process.
        //>= 1 rather than == 1: the counter is a never-reset static, so a GC retry or a future fact reusing
        //this factory must not turn "the finalizer ran" into a confusing count mismatch.
        Assert.True(ThrowingOnReleaseTransformerFactory.ReleaseAttempts >= 1);
    }

    [Fact]
    public void When_an_async_transform_lifetime_scope_finalizer_release_throws_it_should_not_escape()
    {
        //arrange: the async lifetime scope's finalizer runs the same synchronous release path and carries
        //the same guard. Revert the try/catch in ~TransformLifetimeScopeAsync to prove RED.
        CreateAndAbandonLifetimeScopeAsync();

        //act
        CollectAndRunFinalizers();

        //assert: the async scope's finalizer genuinely ran the synchronous release that throws, and it was
        //swallowed rather than escaping ~TransformLifetimeScopeAsync. >= 1 for the same never-reset-static
        //robustness reason as the sync fact above.
        Assert.True(ThrowingOnReleaseTransformerFactoryAsync.ReleaseAttempts >= 1);
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
        var scope = new TransformLifetimeScope(new ThrowingOnReleaseTransformerFactory(), loggerFactory: Initializer.TestLoggerFactory);
        scope.Add(Lease<IAmAMessageTransform>.Untracked(new MinimalTransform()));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndAbandonLifetimeScopeAsync()
    {
        var scope = new TransformLifetimeScopeAsync(new ThrowingOnReleaseTransformerFactoryAsync(), loggerFactory: Initializer.TestLoggerFactory);
        scope.Add(Lease<IAmAMessageTransformAsync>.Untracked(new MinimalTransformAsync()));
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
        private static int s_releaseAttempts;

        //recorded before the throw so the test can prove the finalizer genuinely reached the release
        public static int ReleaseAttempts => Volatile.Read(ref s_releaseAttempts);

        public Lease<IAmAMessageTransform>? Create(Type transformerType) => null;

        public void Release(Lease<IAmAMessageTransform>? lease)
        {
            Interlocked.Increment(ref s_releaseAttempts);
            throw new InvalidOperationException("release failed");
        }
    }

    private sealed class ThrowingOnReleaseTransformerFactoryAsync : IAmAMessageTransformerFactoryAsync
    {
        private static int s_releaseAttempts;

        //recorded before the throw so the test can prove the finalizer genuinely reached the release
        public static int ReleaseAttempts => Volatile.Read(ref s_releaseAttempts);

        public Lease<IAmAMessageTransformAsync>? Create(Type transformerType) => null;

        public void Release(Lease<IAmAMessageTransformAsync>? lease)
        {
            Interlocked.Increment(ref s_releaseAttempts);
            throw new InvalidOperationException("release failed");
        }

        public ValueTask ReleaseAsync(Lease<IAmAMessageTransformAsync>? lease) =>
            throw new InvalidOperationException("release failed");
    }
}
