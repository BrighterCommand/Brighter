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
    /// Used to support reposting a message from a <see cref="IAmAnOutboxSync{T}"/> to a broker via <see cref="IAmAMessageProducerSync"/>
    /// </summary>
    public class MessageRecoverer : IAmAMessageRecoverer
    {
        /// <summary>
        /// Repost the messages with these ids
        /// </summary>
        /// <param name="messageIds">The list of Ids to repost</param>
        /// <param name="outBox">An outbox that holds the messages that we want to resend</param>
        /// <param name="messageProducerSync">A message producer with which to send via a broker</param>
        /// <typeparam name="T">The type of the message</typeparam>
        /// <typeparam name="TTransaction">The type of transaction supported by the outbox</typeparam>
        public void Repost<T, TTransaction>(List<string> messageIds, IAmAnOutboxSync<T, TTransaction> outBox, IAmAMessageProducerSync messageProducerSync)
            where T : Message
        {
            var foundMessages = GetMessagesFromOutBox(outBox, messageIds);
            foreach (var foundMessage in foundMessages)
            {
                messageProducerSync.Send(foundMessage);
            }
        }

        /// <summary>
        /// Gets the selected messages from the store
        /// </summary>
        /// <param name="outBox">The store to retrieve from</param>
        /// <param name="messageIds">The messages to retrieve</param>
        /// <typeparam name="T">The type of the message</typeparam>
        /// <typeparam name="TTransaction">The type of transaction supported by the outbox</typeparam>
        /// <returns>The selected messages</returns>
        private static IEnumerable<Message> GetMessagesFromOutBox<T, TTransaction>(IAmAnOutboxSync<T, TTransaction> outBox, IReadOnlyCollection<string> messageIds)
            where T : Message
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
