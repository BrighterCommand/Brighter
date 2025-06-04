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

using System;

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
    public interface IAmAMessageTransform : IDisposable
    {
        /// <summary>
        /// Gets or sets the context. Usually the context is given to you by the pipeline and you do not need to set this
        /// </summary>
        /// <value>The context.</value>
        IRequestContext? Context { get; set; }
        
        /// <summary>
        /// Initializes from the <see cref="TransformAttribute"/> wrap attribute parameters. Use when you need to provide parameter information from the
        /// attribute to the transform. Note that the attribute implementation might include types other than primitives that you intend to pass across, but
        /// the attribute itself can only use primitives.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        void InitializeWrapFromAttributeParams(params object?[] initializerList);
        
        /// <summary>
        /// Initializes from the <see cref="TransformAttribute"/> unwrap attribute parameters. Use when you need to provide parameter information from the
        /// attribute to the transform. Note that the attribute implementation might include types other than primitives that you intend to pass across, but
        /// the attribute itself can only use primitives.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        void InitializeUnwrapFromAttributeParams(params object?[] initializerList);

        /// <summary>
        /// A Wrap modifies an outgoing message by altering its header or body
        /// A Wrap always runs after you map the <see cref="IRequest"/> to a <see cref="Message"/>
        /// </summary>
        /// <param name="message">The original message</param>
        /// <param name="publication">The publication for the channel that the message is being published to; useful for metadata</param>
        /// <returns>The modified message</returns>
        Message Wrap(Message message, Publication publication);

        /// <summary>
        /// An Unwrap modifies an incoming message by altering its header or body
        /// An Unwrap always runs before you map the <see cref="Message"/> to a <see cref="IRequest"/>
        /// </summary>
        /// <param name="message">The original message</param>
        /// <returns>The modified message</returns>
        Message Unwrap(Message message);
    }
}
