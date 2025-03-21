#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
    /// Interface IAmAnInbox
    /// A Inbox stores <see cref="Command"/>s for diagnostics or replay.
    /// </summary>
    public interface IAmAnInboxSync : IAmAnInbox
    {
        /// <summary>
        ///   Adds a command to the store.
        ///   Will block, and consume another thread for callback on threadpool; use within sync pipeline only.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The command.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        void Add<T>(T command, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest;

        /// <summary>
        ///   Finds a command with the specified identifier.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <returns><see cref="T"/></returns>
        T Get<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest;

        /// <summary>
        ///   Checks whether a command with the specified identifier exists in the store.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The identifier.</param>
        /// <param name="contextKey">An identifier for the context in which the command has been processed (for example, the name of the handler)</param>
        /// <param name="requestContext">What is the context for this request; used to access the Span</param>
        /// <param name="timeoutInMilliseconds">Timeout in milliseconds; -1 for default timeout</param>
        /// <returns><see langword="true"/> if it exists, otherwise <see langword="false"/>.</returns>
        bool Exists<T>(string id, string contextKey, RequestContext? requestContext, int timeoutInMilliseconds = -1) where T : class, IRequest;
    }
}
