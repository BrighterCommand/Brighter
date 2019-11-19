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
    /// NoOp Outbox- a pass-through class for the IAmAOutbox interface(s)
    /// </summary>
    public class NoOpOutbox : IAmAnOutbox<Message>, IAmAnOutboxAsync<Message>,
        IAmAnOutboxViewer<Message>, IAmAnOutboxViewerAsync<Message>
    {
        /// <summary>
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        /// Stub for adding a Message to the outbox. No action is taken
        /// </summary>
        /// <param name="message">Message to add. Will be ignored.</param>
        /// <param name="outBoxTimeout"></param>
        public void Add(Message message, int outBoxTimeout = -1)
        {
        }

        /// <summary>
        /// Stub for adding a Message to the Outbox async
        /// </summary>
        /// <param name="message">Message to add. Will be ignored. </param>
        /// <param name="outBoxTimeout">Timeout</param>
        /// <param name="cancellationToken">Cancelation Token for async operation</param>
        /// <returns>Task.FromResult<object>(null)</object></returns>
        public Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<object>(null);
        }

      /// <summary>
       /// Get the messages that have been marked as flushed in the store
       /// </summary>
       /// <param name="millisecondsDispatchedSince">How long ago would the message have been dispatched in milliseconds</param>
       /// <param name="pageSize">How many messages in a page</param>
       /// <param name="pageNumber">Which page of messages to get</param>
       /// <param name="outboxTimeout"></param>
       /// <param name="args">Additional parameters required for search, if any</param>
       /// <returns>A list of dispatched messages</returns>
       public IEnumerable<Message> DispatchedMessages(
          double millisecondsDispatchedSince, 
          int pageSize = 100, 
          int pageNumber = 1,
          int outboxTimeout = -1, 
          Dictionary<string, object> args = null)
        {
            return new List<Message>();
        }

        /// <summary>
        /// Stub for Getting a message async
        /// </summary>
        /// <param name="messageId">Id of Message to Get</param>
        /// <param name="outBoxTimeout">Timeout for  OutBox</param>
        /// <param name="cancellationToken">Cancelation token for async operation</param>
        /// <returns>Task.FromResult<Message>(null)</returns>
        public Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<Message>(null);
        }
        

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
 
        public Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, Dictionary<string,object> args = null, CancellationToken cancellationToken = default)
        {
           return Task.FromResult<object>(null);
        }

        /// <summary>
        /// Stub for retrieving a pages list fo Messages
        /// </summary>
        /// <param name="pageSize">size of page of messages</param>
        /// <param name="pageNumber">page number</param>
        /// <param name="args">Additional parameters required for search, if any</param>
         /// <returns>Empty List of Messages</returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object> args = null)
        {
            return new List<Message>();
        }

        /// <summary>
        /// Stub for async paged Get of Message
        /// </summary>
        /// <param name="pageSize">size of page of messages</param>
        /// <param name="pageNumber">page number</param>
        /// <param name="cancellationToken">Caancelation token for Task</param>
         /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>Empty List of Messages</returns>
        public Task<IList<Message>> GetAsync(
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<IList<Message>>(new List<Message>());
        }
        
        /// <summary>
        /// Stub for Getting a message. 
        /// </summary>
        /// <param name="messageId">If of the Message to Get</param>
        /// <param name="outBoxTimeout">Timeout for operation</param>
        /// <returns>Always returns NULL</returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            return null;
        }

        /// <summary>
        /// Mark the message as dispatched
        /// </summary>
        /// <param name="id">The message to mark as dispatched</param>
        /// <exception cref="NotImplementedException"></exception>
        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
            return;
        }

       /// <summary>
        /// Get the outstanding message int the outbox
        /// </summary>
        /// <param name="millSecondsSinceSent"></param>
         /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>Any oustanding messages</returns>
       public IEnumerable<Message> OutstandingMessages(
           double millSecondsSinceSent, 
           int pageSize = 100,
           int pageNumber = 1,
           Dictionary<string, object> args = null)
        {
            return new List<Message>();
        }
   }
}
