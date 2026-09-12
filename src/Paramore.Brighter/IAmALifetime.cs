#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;

namespace Paramore.Brighter
{
    /// <summary>
    /// Tracks the handler instances that a handler pipeline has created.
    /// </summary>
    /// <remarks>
    /// <see cref="HandlerLifetimeScope"/> is the default implementation.
    /// <para>
    /// Also carries the handler pipeline's own <see cref="IAmAScope"/> handle, on <see cref="PipelineScope"/>.
    /// This interface holds that handle; it does not become one, and its own job remains tracking the
    /// handler instances the pipeline has created so they can be released.
    /// </para>
    /// </remarks>
    public interface IAmALifetime : IDisposable
    {
        /// <summary>
        /// The DI scope this handler pipeline resolves from, or null when it has none. Released when
        /// this lifetime scope is released; whether releasing it disposes anything is the handle's own
        /// business. Distinct from this interface's own job, which is tracking handler instances so they
        /// can be released.
        /// </summary>
        IAmAScope? PipelineScope { get; }

        /// <summary>
        /// Adds the specified instance.
        /// </summary>
        /// <param name="instance">The instance.</param>
        void Add(IHandleRequests instance);

        /// <summary>
        /// Adds the specified instance of an async handler.
        /// </summary>
        /// <param name="instance">The instance.</param>
        void Add(IHandleRequestsAsync instance);
    }
}
