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
using System.Linq;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class MessageRecoverer.
    /// Used to support reposting a message from a <see cref="IAmAnOutbox{T}"/> to a broker via <see cref="IAmAMessageProducer"/>
    /// </summary>
    public class MessageRecoverer : IAmAMessageRecoverer
    {
        public void Repost(List<string> messageIds, IAmAnOutbox<Message> outBox, IAmAMessageProducer messageProducer)
        {
            var foundMessages = GetMessagesFromOutBox(outBox, messageIds);
            foreach (var foundMessage in foundMessages)
            {
                messageProducer.Send(foundMessage);
            }
        }

        /// <summary>
        /// Gets the selected messages from the store
        /// </summary>
        /// <param name="outBox">The store to retrieve from</param>
        /// <param name="messageIds">The messages to retrieve</param>
        /// <returns></returns>
        private static IEnumerable<Message> GetMessagesFromOutBox(IAmAnOutbox<Message> outBox, IReadOnlyCollection<string> messageIds)
        {
            IEnumerable<Message> foundMessages = messageIds 
                .Select(messageId => outBox.Get(Guid.Parse(messageId)))
                .Where(fm => fm != null)
                .ToList();

            if (foundMessages.Count() < messageIds.Count)
            {
                throw new IndexOutOfRangeException("Cannot find messages " +
                                          string.Join(",", messageIds.Where(id => foundMessages.All(fm => fm.Id.ToString() != id.ToString())).ToArray()));
            }
            return foundMessages;
        }
    }
}