#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Shared drain sequence for the transform pipelines' disposal. A pipeline disposes its transform scope
    /// and then releases its mapper lease; the two are ordered so a failure in the first cannot orphan the
    /// second, and both failures are surfaced rather than the mapper-release masking the scope error. This
    /// helper holds that ordering and error-composition in one place so the sync and async pipelines cannot
    /// drift apart; each caller supplies only the concrete scope-dispose and mapper-release actions.
    /// </summary>
    internal static class TransformPipelineDrain
    {
        /// <summary>
        /// Runs the drain synchronously: dispose the transform scope, then release the mapper, holding any
        /// scope failure so the mapper release still runs and neither error masks the other.
        /// </summary>
        /// <param name="disposeScope">Disposes the transform lifetime scope (a no-op when there is none).</param>
        /// <param name="releaseMapper">Releases the mapper lease back to its registry.</param>
        internal static void Drain(Action disposeScope, Action releaseMapper)
        {
            //hold any transform-scope failure so the mapper release below still runs, but a throw from that
            //release cannot mask it: cleanup must not swallow the real error
            Exception? scopeError = null;
            try
            {
                disposeScope();
            }
            catch (Exception ex)
            {
                scopeError = ex;
            }

            try
            {
                //released unconditionally after the transform scope so a throw from that disposal above cannot
                //orphan the mapper's own scope — the caller's release-once guard is already set, so neither the
                //finalizer nor a later Dispose would retry it, which is the exact leak the pipeline closes
                releaseMapper();
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

        /// <summary>
        /// Runs the drain asynchronously: await the transform scope's disposal, then await the mapper's
        /// release, with the same hold-and-compose error handling as <see cref="Drain"/>.
        /// </summary>
        /// <param name="disposeScopeAsync">Disposes the transform lifetime scope (a no-op when there is none).</param>
        /// <param name="releaseMapperAsync">Releases the mapper lease back to its registry.</param>
        internal static async ValueTask DrainAsync(Func<ValueTask> disposeScopeAsync, Func<ValueTask> releaseMapperAsync)
        {
            //hold any transform-scope failure so the mapper release below still runs, but a throw from that
            //release cannot mask it: cleanup must not swallow the real error
            Exception? scopeError = null;
            try
            {
                await disposeScopeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                scopeError = ex;
            }

            try
            {
                //released unconditionally after the transform scope so a throw from that disposal above cannot
                //orphan the mapper's own scope — the caller's release-once guard is already set, so no later
                //dispose or the finalizer would retry it, which is the exact leak the pipeline closes
                await releaseMapperAsync().ConfigureAwait(false);
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
