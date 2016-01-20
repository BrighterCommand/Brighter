﻿// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : Fred
// Created          : 2016-01-10
//                    Based on IAmAMessageStore.cs
//
// Last Modified By : Fred
// Last Modified On : 2016-01-10
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2016 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Interface IAmAnAsyncMessageStore
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into a Message Store to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// We provide implementations of <see cref="paramore.brighter.commandprocessor.IAmAnAsyncMessageStore{T}"/> for Event Store and SQL. Clients using other message stores should consider a Pull
    /// request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAmAnAsyncMessageStore<in T> : IMessageStore<T> where T : Message
    {
        /// <summary>
        /// Awaitable add the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageStoreTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <returns><see cref="Task"/>.</returns>
        Task AddAsync(T message, int messageStoreTimeout = -1);

        /// <summary>
        /// Awaitable Get the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="messageStoreTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <returns><see cref="Task{Message}"/>.</returns>
        Task<Message> GetAsync(Guid messageId, int messageStoreTimeout = -1);
    }
}
