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
using System.Threading.Tasks;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Interface IAmAMessagePump
    /// The message pump reads <see cref="Message"/>s from a channel, translates them into a <see cref="Request"/>s and asks <see cref="CommandProcessor"/> to
    /// dispatch them to an <see cref="IHandleRequests"/>. It is classical message loop, and so should run until it receives an <see cref="MessageType.MT_QUIT"/>
    /// message. Clients of the message pump need to add a <see cref="Message"/> with of type <see cref="MessageType.MT_QUIT"/> to the <see cref="Channel"/> to abort
    /// a loop once started. The <see cref="IAmAChannelSync"/> interface provides a <see cref="IAmAChannelSync.Stop"/> method for this.
    /// </summary>
    public interface IAmAMessagePump
    {
        /// <summary>
        /// The <see cref="MessagePumpType"/> of this message pump; indicates Reactor or Proactor
        /// </summary>
        MessagePumpType MessagePumpType { get; }

        /// <summary>
        /// Runs the message pump, performing the following:
        /// - Gets a message from a queue/stream
        /// - Translates the message to the local type system
        /// - Dispatches the message to waiting handlers
        /// - Handles any exceptions that occur during the dispatch and tries to keep the pump alive  
        /// </summary>
        /// <exception cref="Exception"></exception>
        void Run();
    }
}
