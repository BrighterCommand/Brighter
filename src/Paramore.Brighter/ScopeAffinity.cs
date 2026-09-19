#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter
{
    /// <summary>
    /// Whether a pipeline should adopt an ambient DI scope its caller already owns, or always create
    /// and own its own.
    /// </summary>
    /// <remarks>
    /// Passed to <see cref="IAmAScopeProvider.GetAmbient"/> so an ambient source can tell what the
    /// asking pipeline needs; the affinity itself is computed by the container-backed factory that
    /// asks, from the configured lifetimes of every factory that participates in the pipeline.
    /// <see cref="AlwaysNew"/> is <c>0</c> so that <c>default(ScopeAffinity)</c> is the safe value.
    /// </remarks>
    public enum ScopeAffinity
    {
        /// <summary>
        /// The pipeline creates and owns its own DI scope. No ambient is adopted, even where one is offered.
        /// </summary>
        AlwaysNew = 0,

        /// <summary>
        /// The pipeline adopts an ambient DI scope its caller already owns, where one is offered and usable.
        /// </summary>
        JoinAmbient
    }
}
