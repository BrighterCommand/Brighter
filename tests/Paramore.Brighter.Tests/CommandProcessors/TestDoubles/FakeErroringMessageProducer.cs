using System;

namespace Paramore.Brighter.Tests.CommandProcessors.TestDoubles
{
    public class FakeErroringMessageProducer : IAmAMessageProducer
    {
        public int SentCalledCount { get; set; }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public void Send(Message message)
        {
            SentCalledCount++;
            throw new Exception();
        }
    }
}