// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-01-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading.Tasks;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Interface IAmAMessageStore
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into a Message Store to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// We provide an implementation of <see cref="IAmAMessageStore{T}"/> for Raven <see cref="RavenMessageStore"/>. Clients using other message stores should consider a Pull
    /// request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAmAMessageStore<in T> where T : Message
    {
        /// <summary>
        /// Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        Task Add(T message);
        /// <summary>
        /// Gets the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        Task<Message> Get(Guid messageId);
    }
}