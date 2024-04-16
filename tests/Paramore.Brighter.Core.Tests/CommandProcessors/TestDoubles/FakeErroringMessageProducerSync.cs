using System;
using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class FakeErroringMessageProducerSync : IAmAMessageProducerSync
    {
        public int SentCalledCount { get; set; }
        public Publication Publication { get; } = new();
        public void Dispose() { }

        public void Send(Message message)
        {
            SentCalledCount++;
            throw new Exception();
        }
        
        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            Send(message);
        }
  
    }
}
