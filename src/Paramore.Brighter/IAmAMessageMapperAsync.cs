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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAMessageMapperAsync
    /// Map between a <see cref="Command"/> or an <see cref="Event"/> and a <see cref="Message"/>. You must implement this for each Command or Message you intend to send over
    /// a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> 
    /// </summary>
    public interface IAmAMessageMapperAsync;

    /// <summary>
    /// Interface IAmAMessageMapperAsync
    /// Map between a <see cref="Command"/> or an <see cref="Event"/> and a <see cref="Message"/>. You must implement this for each Command or Message you intend to send over
    /// a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> 
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    public interface IAmAMessageMapperAsync<TRequest> : IAmAMessageMapperAsync where TRequest : class, IRequest
    {
        /// <summary>
        /// Gets or sets the context. Usually the context is given to you by the pipeline and you do not need to set this
        /// </summary>
        /// <value>The context.</value>
        IRequestContext? Context { get; set; }
        
        /// <summary>
        /// Maps to message.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="publication">The Publication for the channel we are writing the message to, for metadata such as Topic/RoutingKey or CloudEvents</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Message.</returns>
        Task<Message> MapToMessageAsync(TRequest request, Publication publication, CancellationToken cancellationToken = default);

        /// <summary>
        /// Maps to request.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>TRequest.</returns>
        Task<TRequest> MapToRequestAsync(Message message, CancellationToken cancellationToken = default);
    }
}
