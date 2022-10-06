#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// interface IAmAMessageTransformAsync
    /// Derive from this class to provide a transform that modifies a message. It is intended to support re-usable behaviors for a
    /// <see cref="IAmAMessageMapper{TRequest}"/> without using inheritance or DI, and in a consistent fashion.
    /// On Wrap, we assume that you are modifying an outgoing message.
    /// On an Unwrap, we assume that you are modifying an incoming message
    /// A typical usage is the Claim Check pattern see https://www.enterpriseintegrationpatterns.com/patterns/messaging/StoreInLibrary.html
    /// </summary>
    public interface IAmAMessageTransformAsync
    {
        /// <summary>
        /// A Wrap modifies an outgoing message by altering its header or body
        /// A Wrap always runs after you map the <see cref="IRequest"/> to a <see cref="Message"/>
        /// </summary>
        /// <param name="message">The original message</param>
        /// <returns>The modified message</returns>
        Task<Message> Wrap(Message message);
        
        /// <summary>
        /// An Unwrap modifies an incoming message by altering its header or body
        /// An Unwrap always runs before you map the <see cref="Message"/> to a <see cref="IRequest"/>
        /// </summary>
        /// <param name="message">The original message</param>
        /// <returns>The modified message</returns>
        Task<Message> Unwrap(Message message);
    }
}
