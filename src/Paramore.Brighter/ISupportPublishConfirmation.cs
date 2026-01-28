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

namespace Paramore.Brighter
{
    /// <summary>
    /// If this producer support a callback for confirmation of message send then it will return after calling send, but before the message is successfully
    /// persisted to the broker. It will then callback to confirm that the message has persisted to the broker, or not.
    /// Implementing producers should raise the OnMessagePublished event (using a threadpool thread) when the broker returns results
    /// The CommandProcessor will only mark a message as dispatched from the Outbox when this confirmation is received.
    /// </summary>
    public interface ISupportPublishConfirmation
    {
        /// <summary>
        /// Fired when a confirmation is received that a message has been published
        /// bool => was the message published
        /// Guid => what was the id of the published message
        /// </summary>
        event Action<bool, string> OnMessagePublished;
   }
}
