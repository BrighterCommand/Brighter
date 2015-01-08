// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messagestore.ravendb
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-01-2014
// ***********************************************************************
// <copyright file="RavenMessageStore.cs" company="">
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

using System;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;
using Raven.Client;

namespace paramore.brighter.commandprocessor.messagestore.ravendb
{
    /// <summary>
    /// Class RavenMessageStore.
    /// A <see cref="IAmAMessageStore{T}"/> implementation using RavenDB
    /// In order to provide reliability for messages sent over a <a href="http://parlab.eecs.berkeley.edu/wiki/_media/patterns/taskqueue.pdf">Task Queue</a> we
    /// store the message into a Message Store to allow later replay of those messages in the event of failure. We automatically copy any posted message into the store
    /// </summary>
    public class RavenMessageStore : IAmAMessageStore<Message>
    {
        private readonly IDocumentStore documentStore;
        private readonly ILog logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RavenMessageStore"/> class.
        /// </summary>
        /// <param name="documentStore">The document store.</param>
        /// <param name="logger">The logger.</param>
        public RavenMessageStore(IDocumentStore documentStore, ILog logger)
        {
            this.documentStore = documentStore;
            this.logger = logger;
        }

        /// <summary>
        /// Adds the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public async Task Add(Message message)
        {
            logger.Debug(m => m("Adding message to RavenDb Message Store: {0}", JsonConvert.SerializeObject(message)));
            using (var session = documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(message);
                await session.SaveChangesAsync();
                logger.Debug(m => m("Added message to RavenDb"));
            }
        }

        /// <summary>
        /// Gets the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        public Task<Message> Get(Guid messageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                logger.Debug(m => m("Retrieving message with Id {0} from RavenDb", messageId));
                return session.LoadAsync<Message>(messageId).ContinueWith(task => task.Result ?? new Message());
            }
        }
    }
}
