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

namespace Paramore.Brighter
{
    /// <summary>
    /// Class InMemoryRequestContextFactory
    /// Creates a <see cref="RequestContext"/> that can be passed between pipeline stages. The RequestContextFactory created is transient and has a lifetime of the pipeline itself.
    /// </summary>
    public class InMemoryRequestContextFactory : IAmARequestContextFactory
    {
        /// <summary>
        /// Any pipeline has a request context that allows you to flow information between instances of <see cref="IHandleRequests"/>
        /// The default <see cref="InMemoryRequestContextFactory"/> is usable for most cases.
        /// </summary>
        /// <returns>RequestContextFactory.</returns>
        public RequestContext Create()
        {
            return new RequestContext();
        }
    }
}
