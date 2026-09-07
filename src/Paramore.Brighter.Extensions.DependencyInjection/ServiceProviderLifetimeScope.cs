#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Helper class for ServiceProvider-backed factories that provides consistent lifetime handling
    /// for singleton, scoped, and transient object creation. This class extracts the common
    /// lifetime management pattern used across handler, mapper, and transformer factories.
    /// </summary>
    internal sealed partial class ServiceProviderLifetimeScope : IDisposable
    {
        private readonly ILogger _logger;

        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceLifetime _lifetime;
        private readonly ConcurrentDictionary<Type, Lazy<object?>> _singletonInstances = new();
        private readonly ConcurrentDictionary<Type, Lazy<object?>> _scopedInstances = new();
        //every Transient resolution's own scope is tracked here by the scope's own reference identity, NOT by
        //the instance it produced. A resolution IS its scope, so a shared instance (a Singleton resolved under a
        //Transient lifetime) has one distinct entry per resolution rather than several stacked under one key —
        //release keys on the scope, so it reclaims exactly the resolution being released and can never pop a
        //scope another live resolution still holds. The comparer is supplied explicitly: MS DI's own scope
        //happens to use reference equality by default, but IServiceScope carries no such contract, so a custom
        //scope overriding Equals/GetHashCode could otherwise fold two distinct resolutions onto one key and let
        //one release reclaim the other's scope. Used only as a set (value byte is a placeholder); it is the
        //safety net that drains any un-released scope when this scope is disposed.
        private readonly ConcurrentDictionary<IServiceScope, byte> _outstandingScopes =
            new(ReferenceEqualityComparer.Instance);
        private IServiceScope? _scope;
        //when false, the Transient path serves every resolution from one shared scope (the pre-#4254
        //behaviour) instead of giving each resolution its own scope. Handler factories drive this from
        //IBrighterOptions.IsolateTransientHandlerScope; mapper/transformer factories always isolate, so the
        //#4252 leak fix is unaffected by this flag.
        private readonly bool _isolateTransientScopes;
        //an int rather than a bool so Dispose can claim it with a single atomic Interlocked.Exchange,
        //making the disposal body run exactly once even under concurrent Dispose; readers use Volatile.Read
        private int _disposed;

        /// <summary>
        /// Constructs a lifetime scope helper
        /// </summary>
        /// <param name="serviceProvider">The .NET IoC container</param>
        /// <param name="lifetime">The lifetime for created objects</param>
        /// <param name="isolateTransientScopes">
        /// When <c>true</c> (default) each Transient resolution gets its own <see cref="IServiceScope"/>,
        /// released independently; when <c>false</c> every Transient resolution shares one scope that is
        /// disposed with this lifetime scope (the pre-#4254 handler behaviour).
        /// </param>
        public ServiceProviderLifetimeScope(IServiceProvider serviceProvider, ServiceLifetime lifetime, bool isolateTransientScopes = true)
        {
            _serviceProvider = serviceProvider;
            _logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<ServiceProviderLifetimeScope>();
            _lifetime = lifetime;
            _isolateTransientScopes = isolateTransientScopes;
        }

        /// <summary>
        /// Gets the configured lifetime for objects created by this scope
        /// </summary>
        public ServiceLifetime Lifetime => _lifetime;

        /// <summary>
        /// Creates or retrieves an object of the specified type according to the configured lifetime.
        /// - Singleton: Returns the same instance for all calls with the same type
        /// - Scoped: Returns the same instance for all calls with the same type within this scope
        /// - Transient: Creates a new instance for each call (from a scoped provider for proper disposal)
        /// </summary>
        /// <typeparam name="T">The interface type to cast the result to</typeparam>
        /// <param name="objectType">The concrete type to create</param>
        /// <returns>The created or cached instance, or null if not registered</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this scope has already been disposed</exception>
        /// <remarks>
        /// This overload discards the release token, so the caller cannot release an individual transient
        /// resolution's scope — it is drained only when this lifetime scope is disposed. Used by the handler
        /// factory, whose lifetime scope is per-request-pipeline and disposed when the pipeline completes.
        /// A per-message factory (mapper/transformer) that must release each resolution eagerly uses the
        /// <see cref="GetOrCreate{T}(Type, out object?)"/> overload and passes the token to <see cref="Release"/>.
        /// </remarks>
        public T? GetOrCreate<T>(Type objectType) where T : class => GetOrCreate<T>(objectType, out _);

        /// <summary>
        /// Creates or retrieves an object of the specified type according to the configured lifetime, and
        /// returns an opaque <paramref name="releaseToken"/> identifying this resolution.
        /// </summary>
        /// <typeparam name="T">The interface type to cast the result to</typeparam>
        /// <param name="objectType">The concrete type to create</param>
        /// <param name="releaseToken">
        /// For an isolated Transient resolution, the resolution's own <see cref="IServiceScope"/> (as
        /// <see cref="object"/>) — pass it to <see cref="Release"/>/<see cref="ReleaseAsync"/> to drain exactly
        /// that scope. <c>null</c> for Singleton, Scoped, the shared-scope Transient path, and an unresolved
        /// (null) instance — none of which own a per-resolution scope, so release is a no-op.
        /// </param>
        /// <returns>The created or cached instance, or null if not registered</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this scope has already been disposed</exception>
        public T? GetOrCreate<T>(Type objectType, out object? releaseToken) where T : class
        {
            ThrowIfDisposed();

            switch (_lifetime)
            {
                case ServiceLifetime.Singleton:
                    releaseToken = null;
                    return GetOrCreateSingleton<T>(objectType);
                case ServiceLifetime.Scoped:
                    releaseToken = null;
                    return GetOrCreateScoped<T>(objectType);
                case ServiceLifetime.Transient:
                    if (_isolateTransientScopes)
                        return GetTransient<T>(objectType, out releaseToken);
                    releaseToken = null;
                    return GetTransientShared<T>(objectType);
                default:
                    throw new InvalidOperationException($"Unsupported lifetime: {_lifetime}");
            }
        }

        /// <summary>
        /// Gets or creates a singleton instance. Thread-safe using Lazy&lt;T&gt;.
        /// Singletons are shared across all calls for the same type.
        /// </summary>
        private T? GetOrCreateSingleton<T>(Type objectType) where T : class
        {
            var lazy = _singletonInstances.GetOrAdd(objectType, _ =>
                new Lazy<object?>(() => _serviceProvider.GetService(objectType)));
            return (T?)lazy.Value;
        }

        /// <summary>
        /// Gets or creates a scoped instance. Thread-safe using Lazy&lt;T&gt;.
        /// Scoped instances are shared within this scope and disposed when the scope is disposed.
        /// </summary>
        private T? GetOrCreateScoped<T>(Type objectType) where T : class
        {
            EnsureRootScopePublished();

            var lazy = _scopedInstances.GetOrAdd(objectType, _ =>
                new Lazy<object?>(() =>
                {
                    //a concurrent Dispose nulls _scope as it claims it; surface that as an
                    //ObjectDisposedException rather than dereferencing null
                    var scope = _scope;
                    if (scope is null)
                        throw Disposed();
                    return (T?)scope.ServiceProvider.GetService(objectType);
                }));
            return (T?)lazy.Value;
        }

        /// <summary>
        /// Publishes the single shared <c>_scope</c> exactly once, atomically, and reclaims it if a
        /// concurrent <see cref="Dispose"/> raced the publish. Shared by the Scoped path and, when
        /// transient-scope isolation is turned off, the legacy shared-scope Transient path.
        /// </summary>
        private void EnsureRootScopePublished()
        {
            //INVARIANT: publish the first scope with CompareExchange, not `_scope ??= CreateScope()`. Two
            //threads racing the first resolution would each create a scope and the loser's would be
            //overwritten and never disposed (nothing drains a scope that is not _scope). The loser disposes
            //the scope it created.
            if (_scope is not null)
                return;

            var created = _serviceProvider.CreateScope();
            if (Interlocked.CompareExchange(ref _scope, created, null) is not null)
            {
                //lost the publish race — dispose the scope we created
                DisposeScope(created);
            }
            else if (Volatile.Read(ref _disposed) != 0)
            {
                //won the publish, but a concurrent Dispose read _scope before we published and left ours
                //orphaned. Reclaim it (if Dispose has not since claimed it) and throw — the scope this
                //resolution needed is gone. Dispose claims _scope with the same atomic swap, so it is
                //disposed exactly once whichever side wins.
                if (Interlocked.CompareExchange(ref _scope, null, created) == created)
                    DisposeScope(created);
                ThrowIfDisposed();
            }
        }

        /// <summary>
        /// Legacy Transient behaviour (pre-#4254): serves every resolution from one shared
        /// <see cref="IServiceScope"/> that lives until this lifetime scope is disposed, rather than
        /// giving each resolution its own scope. A fresh instance is still returned per call (unlike
        /// Scoped, which caches by type), but all resolutions share one scope, so a scoped-registered
        /// dependency is one shared instance across them. Selected when <c>isolateTransientScopes</c> is
        /// <c>false</c> — the handler path opts back into the per-request-pipeline scope model this way.
        /// </summary>
        /// <remarks>
        /// LEAK INVARIANT — this path is leak-safe only while this <see cref="ServiceProviderLifetimeScope"/>
        /// is short-lived. The shared scope tracks every disposable it resolves and frees them only when the
        /// scope is disposed, so a fresh instance per call accumulates in it until then. That is exactly the
        /// #4252 leak when the scope is app-lifetime — which is why the mapper/transformer factories always
        /// isolate and never reach here. The only caller that sets <c>isolateTransientScopes = false</c> is
        /// <see cref="ServiceProviderHandlerFactory"/>, whose transient lifetime scope is created per
        /// <c>IAmALifetime</c> (one request pipeline) and disposed when that pipeline completes
        /// (<c>ReleaseLifetimeScope</c>), so accumulation is bounded to a single pipeline — the pre-#4254
        /// behaviour, which never leaked. Do not enable this flag on any long-lived lifetime scope.
        /// </remarks>
        private T? GetTransientShared<T>(Type objectType) where T : class
        {
            EnsureRootScopePublished();
            var scope = _scope;
            if (scope is null)
                throw Disposed();
            return (T?)scope.ServiceProvider.GetService(objectType);
        }

        /// <summary>
        /// Creates a transient instance in its own short-lived <see cref="IServiceScope"/>, tracks that scope,
        /// and returns it as the <paramref name="releaseToken"/>. <see cref="Release"/> must be called once per
        /// creation with that token to dispose the scope — without it the scope is retained until this lifetime
        /// scope itself is disposed.
        /// </summary>
        /// <remarks>
        /// The scope is the resolution's identity: it owns more than the instance — also whatever that instance
        /// captured from it, including the scope's own <see cref="IServiceProvider"/>, which the .NET container
        /// injects when a constructor asks for one. Disposing the scope while the instance is still alive hands
        /// the instance a disposed provider, so scope lifetime follows the resolution, not the instance's
        /// disposability, and the scope is tracked even for a non-disposable instance. Only an unresolved (null)
        /// instance leaves nothing to release, so only then is the scope disposed here.
        /// <para>
        /// Tracking is by the scope's own reference, so two resolutions of a shared instance are two distinct
        /// entries; releasing one leaves the other's scope intact. This is what removes the shared-instance
        /// over-release hazard that keyed the old per-instance stack.
        /// </para>
        /// </remarks>
        private T? GetTransient<T>(Type objectType, out object? releaseToken) where T : class
        {
            var scope = _serviceProvider.CreateScope();
            T? instance;
            try
            {
                instance = (T?)scope.ServiceProvider.GetService(objectType);
            }
            catch
            {
                //resolution threw before the scope was tracked (a common misconfiguration: the type
                //is registered but a constructor dependency is not, so the container throws while
                //activating it; the cast can also throw). The scope is not yet tracked, so neither Release
                //nor Dispose could ever reclaim it — dispose it here before rethrowing so a failed
                //resolution does not leak one scope per attempt.
                DisposeScope(scope);
                throw;
            }
            if (instance == null)
            {
                DisposeScope(scope);
                releaseToken = null;
                return null;
            }

            //track the resolution's scope by its own reference (never by the instance): a shared instance
            //gets one entry per resolution, so Release reclaims exactly this one. Add unconditionally: scope
            //is freshly created and reference-unique so a collision cannot occur, but a discarded false from
            //TryAdd would leave this scope untracked yet still handed back as the release token, so neither
            //Release nor Dispose could ever reclaim it — the indexer records it either way.
            _outstandingScopes[scope] = 0;

            //a Dispose that began after our guard drains _outstandingScopes; had it run between our guard
            //check and the TryAdd it would have missed this scope. Re-check: if disposal has started, remove
            //and dispose the scope we just added (TryRemove is atomic, so Dispose's own drain cannot also
            //dispose it) and throw — the factory is tearing down.
            if (Volatile.Read(ref _disposed) != 0)
            {
                if (_outstandingScopes.TryRemove(scope, out _))
                    DisposeScope(scope);
                ThrowIfDisposed();
            }

            releaseToken = scope;
            return instance;
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw Disposed();
        }

        /// <summary>
        /// Builds the <see cref="ObjectDisposedException"/> thrown once this scope is disposed. The type it
        /// names (<see cref="ServiceProviderLifetimeScope"/>) is <c>internal</c>, so on its own it points a
        /// user at nothing they can find. This is the <em>designed</em> failure mode of the owner-disposal
        /// cascade — a Brighter DI-backed mapper/transform/handler factory whose registry was shared across
        /// owners and disposed by one while another is still resolving through it — so the message names the
        /// configured lifetime and the shared-registry cause rather than leaving the operator to guess.
        /// </summary>
        private ObjectDisposedException Disposed() =>
            new(
                $"Brighter DI-backed factory ({_lifetime} lifetime)",
                "The Brighter mapper/transform/handler factory behind this scope has been disposed and can no " +
                "longer resolve objects. This is the designed failure mode when a MessageMapperRegistry (or the " +
                "factories it owns) is shared across owners and one owner disposes it while another is still " +
                "using it. Give each owner its own registry, or do not dispose a shared one until every owner " +
                "is finished with it.");

        /// <summary>
        /// Releases the resolution identified by <paramref name="releaseToken"/> — the token returned by
        /// <see cref="GetOrCreate{T}(Type, out object?)"/> — disposing exactly that resolution's scope. Only
        /// an isolated Transient resolution has a scope of its own; a Singleton (owned by the container), a
        /// Scoped instance (owned by <c>_scope</c> and cached for reuse), the shared-scope Transient path, and
        /// a null token are all no-ops.
        /// <para>
        /// Idempotent: the scope is removed from tracking with an atomic
        /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}.TryRemove(TKey,out TValue)"/>,
        /// so a second release of the same token — or a release racing this scope's disposal — finds nothing to
        /// remove and disposes nothing. Because tracking is per-resolution, this can never dispose a scope
        /// another live resolution still holds, even when both resolutions share one instance.
        /// </para>
        /// Prefer <see cref="ReleaseAsync"/> where the caller can await: this synchronous path can only
        /// suppress the pump context and block on the wait (see <see cref="DisposeScope"/>).
        /// </summary>
        /// <param name="releaseToken">The release token returned alongside the resolution</param>
        public void Release(object? releaseToken)
        {
            if (releaseToken is IServiceScope scope && _outstandingScopes.TryRemove(scope, out _))
                DisposeScope(scope);
        }

        /// <summary>
        /// Releases the resolution identified by <paramref name="releaseToken"/>, draining that scope
        /// asynchronously — the counterpart of <see cref="Release"/>, called from the async pipeline's
        /// <c>DisposeAsync</c>. Awaiting the scope's <c>DisposeAsync</c> rather than blocking on it keeps an
        /// <see cref="IAsyncDisposable"/> mapper/transform from deadlocking the Proactor pump's
        /// single-threaded <see cref="SynchronizationContext"/> (see <see cref="DisposeScope"/>). Shares the
        /// same atomic-remove idempotency as <see cref="Release"/>.
        /// </summary>
        /// <param name="releaseToken">The release token returned alongside the resolution</param>
        public ValueTask ReleaseAsync(object? releaseToken)
        {
            //fast path: a null or non-scope token, or a scope already drained, is a no-op — return a
            //completed ValueTask without allocating an async state machine. This is the common case: the
            //Singleton/Scoped/shared-transient lifetimes carry no token, and every over-release lands here.
            if (releaseToken is IServiceScope scope && _outstandingScopes.TryRemove(scope, out _))
                return DisposeScopeAsync(scope);
            return default;
        }

        /// <summary>
        /// Disposes a service scope synchronously, preferring <see cref="IAsyncDisposable"/> when the
        /// scope offers it.
        /// </summary>
        /// <remarks>
        /// Microsoft's <c>ServiceProviderEngineScope.Dispose()</c> throws
        /// <see cref="InvalidOperationException"/> if it holds a service that implements only
        /// <see cref="IAsyncDisposable"/>, so a scope holding such an instance can only be drained
        /// through <c>DisposeAsync</c>. This method is bound to synchronous signatures
        /// (<see cref="Release"/>, <see cref="Dispose"/>, the pipeline finalizer fallback), so the
        /// returned <see cref="ValueTask"/> is awaited synchronously.
        /// <para>
        /// The hazard is a caller running on a thread a single-threaded
        /// <see cref="SynchronizationContext"/> owns — the Proactor's
        /// <c>BrighterSynchronizationContext</c> — where a user <c>DisposeAsync</c> that awaits without
        /// <c>ConfigureAwait(false)</c> posts its continuation back to that context; blocking here would
        /// then deadlock, because the one thread that could run the continuation is the one we blocked.
        /// So when a <see cref="SynchronizationContext"/> is current we suppress it for the duration of
        /// the disposal: the disposal still runs inline on this thread, but the user's continuations find
        /// no captured context and resume on the thread pool instead of queueing behind our blocking wait.
        /// When no context is current (the Reactor, a finalizer, factory shutdown) there is nothing to
        /// suppress and the same inline path runs. Either way we only fall back to a blocking wait if the
        /// disposal does not complete synchronously — an empty scope (the common case) completes inline as
        /// a no-op, with no thread-pool hop. This still blocks the caller for the disposal's duration —
        /// prefer <see cref="ReleaseAsync"/> where the caller can await.
        /// </para>
        /// <para>
        /// <b>Guidance for mapper/transform authors:</b> a <c>DisposeAsync</c> that performs <b>real
        /// asynchronous I/O</b> (network, disk, a database round-trip) still stalls the releasing thread
        /// for its whole duration whenever release runs synchronously. A mapper/transform
        /// <c>DisposeAsync</c> should release only in-memory state and complete quickly; perform any
        /// genuine I/O elsewhere, never in disposal.
        /// </para>
        /// </remarks>
        /// <param name="scope">The scope to dispose</param>
        private static void DisposeScope(IServiceScope scope)
        {
            if (scope is IAsyncDisposable asyncScope)
            {
                //a captured single-threaded context (BrighterSynchronizationContext) would deadlock a
                //blocking wait: a user DisposeAsync that awaits without ConfigureAwait(false) posts its
                //continuation back to that context, and the pump thread we are about to block is the only
                //one that could run it. Suppress the context for the duration so those continuations
                //resume on the pool instead — no thread-pool hop for the scope itself, and an empty scope
                //(the common case) still completes synchronously as a no-op.
                //LOAD-BEARING INVARIANT: nulling the SynchronizationContext is sufficient only because a
                //pump await captures nothing else. BrighterAsyncContext builds its TaskFactory with
                //TaskCreationOptions/TaskContinuationOptions.HideScheduler, so TaskScheduler.Current is
                //Default inside pump work. If that HideScheduler were dropped, an await would fall back to
                //the cooperating BrighterTaskScheduler (same single pump thread) and this suppression would
                //not prevent the deadlock. See BrighterAsyncContext's constructor for the matching note.
                var previousContext = SynchronizationContext.Current;
                if (previousContext is not null)
                    SynchronizationContext.SetSynchronizationContext(null);
                try
                {
                    var pending = asyncScope.DisposeAsync();
                    if (pending.IsCompleted)
                        pending.GetAwaiter().GetResult();
                    else
                        pending.AsTask().GetAwaiter().GetResult();
                }
                finally
                {
                    if (previousContext is not null)
                        SynchronizationContext.SetSynchronizationContext(previousContext);
                }
                return;
            }
            scope.Dispose();
        }

        /// <summary>
        /// Disposes a service scope asynchronously, preferring <see cref="IAsyncDisposable"/> when the
        /// scope offers it. Used by <see cref="ReleaseAsync"/> so an <see cref="IAsyncDisposable"/>
        /// mapper/transform is awaited rather than blocked on.
        /// </summary>
        /// <param name="scope">The scope to dispose</param>
        private static async ValueTask DisposeScopeAsync(IServiceScope scope)
        {
            if (scope is IAsyncDisposable asyncScope)
            {
                await asyncScope.DisposeAsync().ConfigureAwait(false);
                return;
            }
            scope.Dispose();
        }

        /// <summary>
        /// Disposes of the scope, cleaning up any service scopes and cached instances.
        /// </summary>
        public void Dispose()
        {
            //claim disposal atomically so the body runs exactly once even if two threads race Dispose.
            //The exchange also publishes _disposed before we drain, so a concurrent GetOrCreate either
            //fails its guard or, if it slipped past, sees it on its post-add re-check and cleans up the
            //scope it just tracked.
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try
            {
                //drain every un-released transient scope even when one disposal throws (MS DI's scope Dispose
                //throws for an IAsyncDisposable-only service, and a user Dispose may). This is the terminal
                //cleanup — no finalizer retries it — so a per-scope catch keeps one failure from skipping the
                //rest and from skipping the root-scope disposal in the finally. TryRemove so a concurrent
                //Release of the same scope cannot also dispose it.
                foreach (var scope in _outstandingScopes.Keys)
                {
                    if (!_outstandingScopes.TryRemove(scope, out _))
                        continue;
                    //best-effort cleanup: a throw must not skip the remaining scopes, but it is logged
                    //(a repeated failure on this terminal teardown path means an unbounded leak) rather
                    //than swallowed silently, matching the other release paths in this change.
                    try
                    { DisposeScope(scope); }
                    catch (Exception e) { Log.FailedToDisposeScope(_logger, e); }
                }
            }
            finally
            {
                //claim _scope with an atomic swap so it is disposed exactly once whichever side wins the race
                //with a concurrent first-scope publish (which re-checks _disposed and reclaims what it
                //published). In the finally so a throwing transient drain above cannot strand it undisposed.
                var rootScope = Interlocked.Exchange(ref _scope, null);
                if (rootScope != null)
                {
                    //logged for the same reason as the transient drain above — best-effort, but not silent
                    try
                    { DisposeScope(rootScope); }
                    catch (Exception e) { Log.FailedToDisposeScope(_logger, e); }
                }
                _scopedInstances.Clear();
            }
            // Note: Don't clear singleton instances as they may be shared
        }

        /// <summary>
        /// Keys <see cref="_outstandingScopes"/> on the scope's own reference identity, independent of any
        /// <c>Equals</c>/<c>GetHashCode</c> a scope implementation might override. <c>ReferenceEqualityComparer</c>
        /// from the BCL is not available on <c>netstandard2.0</c>, so this supplies the same behaviour.
        /// </summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<IServiceScope>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public bool Equals(IServiceScope? x, IServiceScope? y) => ReferenceEquals(x, y);

            public int GetHashCode(IServiceScope obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "Failed to dispose a service scope while tearing down the Brighter DI-backed factory. Best-effort cleanup continues; a repeated failure here points at a mapper/transform/handler Dispose that throws.")]
            public static partial void FailedToDisposeScope(ILogger logger, Exception exception);
        }
    }
}
