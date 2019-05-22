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
    /// Interface IAmAControlBusSenderFactory. Helper for creating a control bus sender, which only requires
    /// messaging configuration because it wraps the command processor and only supports the Post method, 
    /// not Send and Publish and as such does not have handlers to register
    /// </summary>
    public interface IAmAControlBusSenderFactory {
        /// <summary>
        /// Creates the specified configuration.
        /// </summary>
        /// <param name="gateway">The gateway to the control bus</param>
        /// <param name="logger">The logger to use</param>
        /// <param name="outbox">The outbox to record outbound messages on the control bus</param>
        /// <returns>IAmAControlBusSender.</returns>
        IAmAControlBusSender Create(IAmAnOutbox<Message> outbox, IAmAMessageProducer gateway);
    }
}
