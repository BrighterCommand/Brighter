// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ip
// Created          : 07-19-2016
//
// Last Modified By : ip
// Last Modified On : 07-19-2016
// ***********************************************************************
// <copyright file="NoOpMessageStore.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// NoOp Message Store - a pass-through class for the IAmAMessageStore interface(s)
    /// </summary>
    public class NoOpMessageStore : IAmAMessageStore<Message>, IAmAMessageStoreAsync<Message>,
        IAmAMessageStoreViewer<Message>, IAmAMessageStoreViewerAsync<Message>
    {
        /// <summary>
        /// Stub for adding a Message to the message store. No action is taken
        /// </summary>
        /// <param name="message">Message to add. Will be ignored.</param>
        /// <param name="messageStoreTimeout"></param>
        public void Add(Message message, int messageStoreTimeout = -1)
        {
        }

        /// <summary>
        /// Stub for Getting a message. 
        /// </summary>
        /// <param name="messageId">If of the Message to Get</param>
        /// <param name="messageStoreTimeout">Timeout for operation</param>
        /// <returns>Always returns NULL</returns>
        public Message Get(Guid messageId, int messageStoreTimeout = -1)
        {
            return null;
        }

        /// <summary>
        /// Stub for adding a Message to the Message Store async
        /// </summary>
        /// <param name="message">Message to add. Will be ignored. </param>
        /// <param name="messageStoreTimeout">Timeout</param>
        /// <param name="cancellationToken">Cancelation Token for async operation</param>
        /// <returns>Task.FromResult<object>(null)</object></returns>
        public Task AddAsync(Message message, int messageStoreTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<object>(null);
        }

        /// <summary>
        /// Stub for Getting a message async
        /// </summary>
        /// <param name="messageId">Id of Message to Get</param>
        /// <param name="messageStoreTimeout">Timeout for message store</param>
        /// <param name="cancellationToken">Cancelation token for async operation</param>
        /// <returns>Task.FromResult<Message>(null)</returns>
        public Task<Message> GetAsync(Guid messageId, int messageStoreTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<Message>(null);
        }

        /// <summary>
        /// 
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        /// Stub for retrieving a pages list fo Messages
        /// </summary>
        /// <param name="pageSize">size of page of messages</param>
        /// <param name="pageNumber">page number</param>
        /// <returns>Empty List of Messages</returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1)
        {
            return new List<Message>();
        }

        /// <summary>
        /// Stub for async paged Get of Message
        /// </summary>
        /// <param name="pageSize">size of page of messages</param>
        /// <param name="pageNumber">page number</param>
        /// <param name="cancellationToken">Caancelation token for Task</param>
        /// <returns>Empty List of Messages</returns>
        public Task<IList<Message>> GetAsync(int pageSize = 100, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<IList<Message>>(new List<Message>());
        }
    }
}