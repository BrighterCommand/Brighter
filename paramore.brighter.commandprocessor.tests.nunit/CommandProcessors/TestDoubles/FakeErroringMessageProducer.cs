using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles
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