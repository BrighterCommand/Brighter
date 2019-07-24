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
using System.Collections.Generic;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAMessageStoreViewer
    /// In order to provide monitoring of messages in an OutBox  to allow later replay of those messages in the event of failure. 
    /// We provide an implementation of <see cref="IAmAnOutboxViewer{T}"/> for Raven <see cref="RavenMessageStore"/>. Clients using other message stores should consider a Pull
    /// request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAmAnOutboxViewer<T> where T : Message
    {
        /// <summary>
        /// Retrieves messages that have been sent within the window
        /// </summary>
        /// <param name="millisecondsDispatchedSince"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns></returns>
        IEnumerable<Message> DispatchedMessages(
            double millisecondsDispatchedSince, 
            int pageSize = 100, 
            int pageNumber = 1, 
            int outboxTimeout = -1,
            Dictionary<string, object> args = null);

        /// <summary>
        /// Gets all messages in the OutBox, LIFO
        /// </summary>
        /// <param name="pageSize">number of items on the page, default is 100</param>
        /// <param name="pageNumber">page number of results to return, default is first</param>
        /// <param name="args">Additional parameters required for the search, if any</param>
        /// <returns></returns>
        IList<T> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object> args = null);
        
        /// <summary>
        /// Messages still outstanding in the Outbox because their timestamp
        /// </summary>
        /// <param name="millSecondsSinceSent">How many seconds since the message was sent do we wait to declare it outstanding</param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
         /// <returns>Outstanding Messages</returns>
        IEnumerable<Message> OutstandingMessages(
            double millSecondsSinceSent, 
            int pageSize = 100, 
            int pageNumber = 1,
            Dictionary<string, object> args = null);
    }

    public enum MessageStoreType
    {
        SqlCe,
        RavenRemote,
        RavenLocal,
        SqlServer
    }
}
