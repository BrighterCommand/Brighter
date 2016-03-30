// ***********************************************************************
// Assembly         : paramore.brighter.serviceactivator
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-10-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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

using System.Threading.Tasks;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.serviceactivator
{
    /// <summary>
    /// Interface IAmAMessagePump
    /// The message pump reads <see cref="Message"/>s from a channel, translates them into a <see cref="Request"/>s and asks <see cref="CommandProcessor"/> to
    /// dispatch them to an <see cref="IHandleRequests"/>. It is classical message loop, and so should run until it receives an <see cref="MessageType.MT_QUIT"/>
    /// message. Clients of the message pump need to add a <see cref="Message"/> with of type <see cref="MessageType.MT_QUIT"/> to the <see cref="Channel"/> to abort
    /// a loop once started. The <see cref="IAmAChannel"/> interface provides a <see cref="IAmAChannel.Stop"/> method for this.
    /// </summary>
    public interface IAmAMessagePump
    {
        /// <summary>
        /// Runs the message loop
        /// </summary>
        Task Run();
        /// <summary>
        /// Gets or sets the timeout in milliseconds, that the pump waits for a message on the queue before it yields control for an interval, prior to resuming.
        /// </summary>
        /// <value>The timeout in milliseconds.</value>
        int TimeoutInMilliseconds { get; set; }
        /// <summary>
        /// Gets or sets the channel to read messages from.
        /// </summary>
        /// <value>The channel.</value>
        IAmAChannel Channel { get; set; }
        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>The logger.</value>
        ILog Logger { get; set; }
    }
}