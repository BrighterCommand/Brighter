using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public abstract class TransformPipelineAsync<TRequest> : IDisposable, IAsyncDisposable where TRequest : class, IRequest
    {
        //the mapper lease keys release on the resolution the registry opened, not the mapper instance, so a
        //shared mapper is reclaimed one resolution at a time
        protected readonly Lease<IAmAMessageMapperAsync<TRequest>> MapperLease;
        protected readonly IReadOnlyList<Lease<IAmAMessageTransformAsync>> TransformLeases;
        protected readonly IReadOnlyList<IAmAMessageTransformAsync> Transforms;
        protected IAmAMessageMapperAsync<TRequest> MessageMapper => MapperLease.Instance;
        protected TransformLifetimeScopeAsync? InstanceScope;

        private readonly IAmAMessageMapperRegistryAsync? _mapperRegistry;
        private int _released;

        protected TransformPipelineAsync(
            Lease<IAmAMessageMapperAsync<TRequest>> messageMapperLease,
            IEnumerable<Lease<IAmAMessageTransformAsync>> transformLeases,
            IAmAMessageMapperRegistryAsync? mapperRegistry = null)
        {
            MapperLease = messageMapperLease ?? throw new ArgumentNullException(nameof(messageMapperLease));
            TransformLeases = transformLeases as IReadOnlyList<Lease<IAmAMessageTransformAsync>> ?? transformLeases.ToArray();
            //materialise the transform instances once for execution; the leases stay for release
            Transforms = TransformLeases.Select(lease => lease.Instance).ToArray();
            _mapperRegistry = mapperRegistry;
        }

        /// <summary>
        /// Disposes the pipeline, releasing the message mapper and any transforms back to their factories.
        /// <para>
        /// This is the synchronous fallback — the finalizer, or a caller that used <c>using</c> rather
        /// than <c>await using</c>. On a thread owned by the Proactor's single-threaded synchronization
        /// context prefer <see cref="DisposeAsync"/>: releasing an <see cref="IAsyncDisposable"/>
        /// mapper/transform synchronously drains it through a blocking wait (the pump context is suppressed
        /// for the wait to avoid deadlock, but it is still a stall), whereas <see cref="DisposeAsync"/>
        /// awaits it without blocking the pump.
        /// </para>
        /// </summary>
        public void Dispose()
        {
            //SuppressFinalize in a finally: if the release throws (explicit Dispose still surfaces it),
            //the object would otherwise stay registered for finalization, and the finalizer's retry only
            //returns on the release-once guard — a wasted finalization
            try { ReleaseUnmanagedResources(); }
            finally { GC.SuppressFinalize(this); }
        }

        /// <summary>
        /// Disposes the pipeline asynchronously, releasing the message mapper and any transforms back to
        /// their factories and awaiting their disposal. Preferred on the Proactor pump thread: an
        /// <see cref="IAsyncDisposable"/> mapper/transform is awaited rather than blocked on, so a
        /// continuation its <c>DisposeAsync</c> posts back to the single-threaded synchronization context
        /// can run.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            //release once only; shares the guard with Dispose so an explicit dispose followed by another
            //(in either form) must not release twice
            if (Interlocked.Exchange(ref _released, 1) != 0) return;

            //SuppressFinalize in an outer finally so it runs even if the release throws (an explicit
            //DisposeAsync still surfaces the exception); otherwise the object stays registered for
            //finalization and the finalizer's retry only returns on the release-once guard — wasted work
            try
            {
                //hold any transform-scope failure so the mapper release below still runs, but a throw from
                //that release cannot mask it: cleanup must not swallow the real error
                Exception? scopeError = null;
                try
                {
                    if (InstanceScope is not null)
                        await InstanceScope.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scopeError = ex;
                }

                try
                {
                    //the mapper is created per pipeline, so it is ours to return; released outside InstanceScope
                    //because that scope only exists when a transformer factory was supplied. Released
                    //unconditionally after the transform scope so a throw from that disposal above cannot orphan
                    //the mapper's own scope — the release-once guard is already set, so no later dispose or the
                    //finalizer would retry it, which is the exact leak this pipeline is meant to close.
                    if (_mapperRegistry is not null)
                        await _mapperRegistry.ReleaseAsync(MapperLease).ConfigureAwait(false);
                }
                catch (Exception releaseError)
                {
                    //both failed: surface both rather than let this cleanup exception mask the transform-scope one
                    if (scopeError is null) throw;
                    throw new AggregateException(scopeError, releaseError);
                }

                //only the transform scope failed: rethrow it preserved (type and stack, including its own drain
                //AggregateException)
                if (scopeError is not null) ExceptionDispatchInfo.Capture(scopeError).Throw();
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Disposes a pipeline builder, which will call release on the factory for the message mapper and
        /// any transforms generated for the pipeline
        /// </summary>
        ~TransformPipelineAsync()
        {
            //a finalizer must never let an exception escape — that terminates the process. Releasing a
            //scope that holds an IAsyncDisposable-only mapper/transform through the synchronous path can
            //throw (netstandard2.0 only: MS DI's sync scope Dispose throws for an async-only service; net8+
            //takes the async branch first), as can a user
            //Dispose/DisposeAsync. Release best-effort here and swallow; an explicit Dispose/DisposeAsync
            //still surfaces the exception to the caller who owns the pipeline.
            try { ReleaseUnmanagedResources(); }
            catch { /* swallowed: a finalizer must not throw */ }
        }

        private void ReleaseUnmanagedResources()
        {
            //release once only; an explicit Dispose followed by another must not release twice
            if (Interlocked.Exchange(ref _released, 1) != 0) return;

            //hold any transform-scope failure so the mapper release below still runs, but a throw from that
            //release cannot mask it: cleanup must not swallow the real error
            Exception? scopeError = null;
            try
            {
                InstanceScope?.Dispose();
            }
            catch (Exception ex)
            {
                scopeError = ex;
            }

            try
            {
                //the mapper is created per pipeline, so it is ours to return; released outside InstanceScope
                //because that scope only exists when a transformer factory was supplied. Released
                //unconditionally after the transform scope so a throw from that disposal above cannot orphan
                //the mapper's own scope — the release-once guard is already set, so neither the finalizer nor
                //a later Dispose would retry it, which is the exact leak this pipeline is meant to close.
                _mapperRegistry?.Release(MapperLease);
            }
            catch (Exception releaseError)
            {
                //both failed: surface both rather than let this cleanup exception mask the transform-scope one
                if (scopeError is null) throw;
                throw new AggregateException(scopeError, releaseError);
            }

            //only the transform scope failed: rethrow it preserved (type and stack, including its own drain
            //AggregateException)
            if (scopeError is not null) ExceptionDispatchInfo.Capture(scopeError).Throw();
        }
    }
}
