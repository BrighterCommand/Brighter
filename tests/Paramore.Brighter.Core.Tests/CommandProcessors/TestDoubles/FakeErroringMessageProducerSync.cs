using System;
using System.Diagnostics;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class FakeErroringMessageProducerSync : IAmAMessageProducerSync
    {
        public int SentCalledCount { get; set; }
        public Publication Publication { get; } = new();
        
        public Activity Span { get; set; }
        public IAmAMessageScheduler? Scheduler { get; set; }

        public void Dispose() { }

        public void Send(Message message)
        {
            SentCalledCount++;
            throw new Exception();
        }
        
        public void SendWithDelay(Message message, TimeSpan? delay = null)
        {
            Send(message);
        }
  
    }
}
