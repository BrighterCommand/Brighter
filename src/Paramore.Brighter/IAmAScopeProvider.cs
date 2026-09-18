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
    /// An ambient source. Answers whether there is a DI scope the calling application already owns
    /// that this pipeline may resolve from. It supplies no scope of its own: whatever it does not
    /// offer, the container package creates and owns.
    /// </summary>
    /// <remarks>
    /// Implemented by a container package outside Brighter's own DI package - for example, one backed
    /// by <c>IHttpContextAccessor</c> - and consulted by each container-backed factory's
    /// <c>CreatePipelineScope()</c>. Creates nothing, owns nothing, disposes nothing: whatever ambient
    /// this returns remains the caller's to dispose, never Brighter's.
    /// <para>
    /// A throw from <see cref="GetAmbient"/> is a startup-class fault, not an ordinary build failure.
    /// The calling factory wraps it in <see cref="AmbientScopeSourceException"/> so it reaches the
    /// caller of <c>Send</c>/<c>Publish</c>/<c>Post</c> unwrapped, rather than being folded into a
    /// <see cref="ConfigurationException"/> like any other build failure.
    /// </para>
    /// </remarks>
    public interface IAmAScopeProvider
    {
        /// <summary>
        /// Answers whether there is an ambient DI scope this pipeline may adopt.
        /// </summary>
        /// <param name="affinity">The affinity of the pipeline that is asking, computed by the caller
        /// over the whole participating set.</param>
        /// <returns>An ambient the pipeline may adopt, or <see langword="null"/> where there is none.
        /// Returning <see langword="null"/> is not an error; it is the ordinary answer. An ambient
        /// returned for an <see cref="ScopeAffinity.AlwaysNew"/> ask violates this contract and is
        /// ignored rather than trusted.</returns>
        IAmAScope? GetAmbient(ScopeAffinity affinity);
    }
}
